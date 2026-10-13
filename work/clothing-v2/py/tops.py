"""Tops: polo (tennis / golf, set-in or raglan) and the racerback tank. Shell = region of the real body -> smooth envelope (no muscle
detail) -> hang rules (fabric falls from chest/shoulder blades, sleeves stand off the arm) -> clean isoline hems."""
import math
import numpy as np
import glib as G


def arm_frame(body, side):
    s = 'Left' if side < 0 else 'Right'
    sh = body.head(s + 'UpperArm'); el = body.head(s + 'LowerArm')
    return sh, (el - sh) / np.linalg.norm(el - sh)


def torso_axis(body):
    """vertical torso axis through hips .. neck (x = 0, y = mean of bone heads), plus front direction (-Y)."""
    zs = np.array([body.head(b)[2] for b in ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck']])
    ys = np.array([body.head(b)[1] for b in ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck']])
    return zs, ys


def arm_s(body, x, side):
    sh, ax = arm_frame(body, side)
    return (x - sh) @ ax


def shell_region(body, z_lo, z_hi, sleeve_len, margin=.04):
    """body faces of the top: torso between z_lo and z_hi + the upper arms up to sleeve_len (+margin) along the arm axis.
    Returns pos, tri and per-vertex arm membership (-1 left arm, +1 right arm, 0 torso)."""
    p = body.pos
    sL = arm_s(body, p, -1); sR = arm_s(body, p, 1)
    armL = body.armL > .5; armR = body.armR > .5
    ok = (p[:, 2] > z_lo) & (p[:, 2] < z_hi) & (body.headw < .3) & (body.forearmL < .5) & (body.forearmR < .5) & (body.legL < .9) & (body.legR < .9)
    ok &= ~(armL & (sL > sleeve_len + margin)) & ~(armR & (sR > sleeve_len + margin))
    t = body.tri[ok[body.tri].all(1)]
    side = np.where(armL, -1, np.where(armR, 1, 0)).astype(np.float64)
    r = G.compact(body.pos, t, side, body.W)
    r = G.largest_component(r[0], r[1], r[2], r[3])
    pos_, tri_, side_, W_ = G.fill_tiny_holes(r[0], r[1], .035, r[2], r[3])
    body._last_W = W_                   # skin weights of the vertices the shell grows from (travel with the shell through cuts)
    return pos_, tri_, np.round(side_)


def polo(body, P):
    """P: design numbers (metres). Returns the solved outer shell (pos, tri) and per-vertex side (-1/0/+1)."""
    z_hem, L = P['z_hem'], P['sleeve']
    pos, tri, side = shell_region(body, z_hem - .06, P['z_neck'] + .05, L)
    zs, ys = torso_axis(body)
    torso_origin = lambda s: np.stack([np.zeros_like(s), np.interp(s, zs, ys), s], 1)
    up = np.array([0, 0, 1.]); fwd = np.array([0, -1., 0])
    bmask = (body.armL < .3) & (body.armR < .3) & (body.headw < .5)
    z_top = P['z_chest_top']
    tenv, tframe = G.build_envelope(body, torso_origin, up, fwd, z_hem - .08, z_top, .01, bmask)
    tenv.smooth(iters=3)
    import copy
    raw = copy.deepcopy(tenv)
    if P.get('g_chest'):
        # PATTERN FIRST: the shirt's own cut (girths in metres) hanging plumb from the chest; the body only pushes it out where it is bigger
        zc_ = P['z_chest']; c0 = G.hull_centre(raw, zc_); a0 = G.hull_aspect(raw, zc_)
        zw = P.get('z_waist_pattern', (P['z_hem'] + zc_) / 2)
        gz = [P['z_hem'] - .12, P['z_hem'], zw, zc_, z_top + .1]
        gg = [P['g_hem'], P['g_hem'], P['g_waist'], P['g_chest'], P['g_chest']]
        tenv = G.pattern_envelope(raw, lambda s_: float(np.interp(s_, gz, gg)), lambda s_: a0 * P.get('aspect_scale', 1.0),
                                  lambda s_: c0, P['clear'] + .003, smooth=2)
    else:
        tenv.sweep(int(round((P['z_chest'] - tenv.s0) / tenv.ds)), -1, P['hang_slope']).smooth(iters=2)
    zero = np.zeros(3)
    arms = {}
    for sd in (-1, 1):
        sh, ax = arm_frame(body, sd)
        amask = ((body.armL if sd < 0 else body.armR) > .5) & ((body.forearmL if sd < 0 else body.forearmR) < .5)
        o_fn = (lambda sh, ax: (lambda s: sh[None, :] + np.outer(s, ax)))(sh, ax)
        aenv, aframe = G.build_envelope(body, o_fn, ax, up, P['sleeve_root'], L + .04, .008, amask, nth=96)
        aenv.smooth(iters=2)
        if P.get('g_sleeve'):
            # the sleeve's own cut: from the armhole (hull at the root + room) straight to the designed opening
            j0 = 0; r0 = aenv.R[:, j0]; th_ = aenv.th
            root_g = np.linalg.norm(np.diff(np.stack([r0 * np.cos(th_), r0 * np.sin(th_)], 1), axis=0), axis=1).sum() + P.get('g_armhole_ease', .05)
            cen = G.hull_centre(aenv, L)
            aenv = G.pattern_envelope(aenv, lambda s_: float(np.interp(s_, [P['sleeve_root'], L], [root_g, P['g_sleeve']])), lambda s_: 1.08, lambda s_: cen, P['clear'] + .002, smooth=1)
        else:
            aenv.sweep(0, 1, P['sleeve_slope']).smooth(iters=1)
        arms[sd] = (aenv, aframe, o_fn, sh, ax)

    def env(x):
        z = x[:, 2]
        tm = (side == 0) & (z < z_top)
        blend = np.clip((z_top - z) / P.get('top_blend', .05), 0, 1)
        blend = blend * blend * (3 - 2 * blend)
        x = G.apply_envelope(x, tm, tenv, torso_origin, tframe, zero, P['ease_torso'] + P.get('blouse', 0) * np.exp(-((z - P.get('tuck_z', -9) - .025) / .03) ** 2), blend)
        if P.get('tuck_z'):
            # tucked: below the waistband the shirt is held tight to the body (it lives inside the trousers / skirt)
            tz = P['tuck_z']
            m = (side == 0) & (z < tz + .02)
            if m.any():
                xm = x[m]
                s_, th_, r_, o_ = G.frame_coords(xm, torso_origin, tframe, zero)
                cap = raw(th_, s_) + P['tuck_ease']
                w = np.clip((tz + .02 - z[m]) / .03, 0, 1)
                rn = np.where(r_ > cap, r_ + (cap - r_) * w, r_)
                q = np.outer(np.cos(th_), tframe[1]) + np.outer(np.sin(th_), tframe[2])
                x[m] = o_ + np.outer((xm - o_) @ tframe[0], tframe[0]) + q * rn[:, None]
        for sd, (aenv, aframe, o_fn, sh, ax) in arms.items():
            t = (x - sh) @ ax
            am = (side == sd) & (t > P['sleeve_root'])
            ab = np.clip((t - P['sleeve_root']) / .03, 0, 1)
            x = G.apply_envelope(x, am, aenv, o_fn, aframe, sh, P['ease_sleeve'], ab)
        return x

    clear = np.full(len(pos), P['clear'])
    obs = []
    for (piece, cl, zmin, zmax) in P.get('over', []):
        o = G.obstacle(body.hero.id, piece, cl)
        if o is not None: obs.append((o[0], o[1], (pos[:, 2] > zmin) & (pos[:, 2] < zmax)))
    x = G.solve_shell(body, pos, tri, clear, iters=P.get('iters', 40), env=env, obstacles=obs)
    x = G.relax_tangential(x, tri, 8)
    x = G.solve_shell(body, pos, tri, clear, iters=10, env=env, start=x, obstacles=obs)
    return x, tri, side


def torso_theta(body, x):
    zs, ys = torso_axis(body)
    yc = np.interp(x[:, 2], zs, ys)
    return np.arctan2(x[:, 0], -(x[:, 1] - yc)), np.hypot(x[:, 0], x[:, 1] - yc)


def sleeve_field(body, g, sd, L):
    sh, ax = arm_frame(body, sd)
    t = (g.pos - sh) @ ax
    on = np.abs(g.A['side'] - sd) < .5
    f = np.where(on, L - t, .08)
    f = G.smooth_field(f, g.tri, 10, fixed=on)
    return np.where(on, f, np.maximum(f, .012))      # the torso is never cut by a sleeve hem (no stray holes behind the armpit)


def build_polo(body, P):
    from garment import Garment, solidify, PIQUE, RIB
    import parts
    x, tri, side = polo(body, P)
    g = Garment(x, tri, fabric=PIQUE, roles=('Kit_Shirt',), side=side, W=body._last_W)
    # ---- borders
    th, r = torso_theta(body, g.pos)
    back = (1 - np.cos(th)) * .5
    z_hem = P['z_hem'] - P.get('back_drop', .012) * back
    g.A['f_hem'] = g.pos[:, 2] - z_hem
    g.cut(g.A['f_hem'])
    # side vents: a narrow notch at each side seam
    if P.get('vent', .0) > 0:
        for sgn in (-1, 1):
            th, r = torso_theta(body, g.pos)
            d_side = np.abs(np.angle(np.exp(1j * (th - sgn * math.pi / 2)))) * r
            hw = P['vent_w'] * np.clip(1 - g.A['f_hem'] / P['vent'], 0, 1)
            g.cut(d_side - hw + 1e-5 * (g.A['f_hem'] > P['vent']))
    for sd in (-1, 1):
        g.A['f_sl%d' % sd] = sleeve_field(body, g, sd, P['sleeve'])
        g.cut(g.A['f_sl%d' % sd])
    th, r = torso_theta(body, g.pos)
    zn = P['z_neck_front'] + (P['z_neck_back'] - P['z_neck_front']) * (1 - np.cos(th)) * .5
    g.A['f_neck'] = (zn - g.pos[:, 2]) + 2.0 * np.maximum(0, r - P['neck_r'])
    g.cut(g.A['f_neck'])
    g.largest()
    from garment import fill_small_holes
    fill_small_holes(g); fill_small_holes(g)
    # ---- construction lines as geometry: cuff band (+ stripes), hem band
    cuff = P.get('cuff', .025)
    for sd in (-1, 1):
        f = g.A['f_sl%d' % sd]
        for v in [cuff, cuff - .0015] + ([.0035, .0085, .0115, .015] if P.get('tipping') else []):
            g.insert(f - v)
            f = g.A['f_sl%d' % sd]
        f = g.A['f_sl%d' % sd]
        band = (f < cuff - 1e-4) & (np.abs(g.A['side'] - sd) < .5)
        g.A['lift'][band] = np.maximum(g.A['lift'][band], P.get('cuff_lift', .0009) * np.clip((cuff - f[band]) / .0015, 0, 1))
        if P.get('tipping'):
            st = band & (((f > .004) & (f < .0075)) | ((f > .0105) & (f < .013)))
            g.A['trim'][st] = 1
    if P.get('raglan'):
        # raglan seam: from the underarm diagonally up to the collar, front and back, both sides (a seam line: cavity + stitch guide)
        for sd in (-1, 1):
            sh, ax = arm_frame(body, sd)
            for fb in (-1, 1):
                a = np.array([sd * P['raglan_neck_x'], fb * .05 + body.head('Neck')[1], P['z_neck_front'] + .01])
                b = sh + np.array([-sd * .035, fb * .07, -.075])
                nrm = np.cross(b - a, np.array([0, fb, 0.])); nrm /= np.linalg.norm(nrm)
                if nrm @ (sh - a) < 0: nrm = -nrm
                th_, r_ = torso_theta(body, g.pos)
                on = (np.sign(g.pos[:, 1] - body.head('Neck')[1]) == fb) & (np.sign(g.pos[:, 0]) == sd) & (g.pos[:, 2] > b[2] - .02)
                fld = np.where(on, (g.pos - a) @ nrm, 1.0)
                g.insert(fld)
                on = (np.sign(g.pos[:, 1] - body.head('Neck')[1]) == fb) & (np.sign(g.pos[:, 0]) == sd) & (g.pos[:, 2] > b[2] - .02)
                g.A['raglan_d%d%d' % (sd, fb)] = np.where(on, np.abs((g.pos - a) @ nrm), 1.0)
    for v in [.022, .0205]:
        g.insert(g.A['f_hem'] - v)
    hemband = g.A['f_hem'] < .0215
    g.A['lift'][hemband] = np.maximum(g.A['lift'][hemband], .0006)
    # rib faces on the cuffs
    fc = np.zeros(len(g.tri), bool)
    for sd in (-1, 1):
        f = g.A['f_sl%d' % sd]; fc |= (f[g.tri].mean(1) < cuff) & (np.abs(g.A['side'][g.tri].mean(1) - sd) < .5)
    if P.get('rib_cuff', True): g.F['fabric'][fc] = RIB
    loops = G.boundary_loops(g.tri)
    neck_loop = max(loops, key=lambda l: g.pos[l, 2].mean())
    sf = g.A['side'][g.tri].mean(1)
    g.F['proj'] = np.where(sf < -.5, 1, np.where(sf > .5, 2, 0)).astype(np.int32)
    from garment import smooth_borders
    smooth_borders(g)
    shell = solidify(g, P.get('thickness', .0025), facing=.03)
    # ---- collar, placket, buttons
    parts_ = []
    col = parts.polo_collar(body, g, neck_loop, P)
    if P.get('tipping'):
        e = col.A['collar_edge']
        for v in [.0035, .0085, .0115, .015]:
            col.insert(col.A['collar_edge'] - v)
        e = col.A['collar_edge']
        col.A['trim'][((e > .004) & (e < .0075)) | ((e > .0105) & (e < .013))] = 1
    parts_.append(solidify(col, P.get('thickness', .0025) * .9, facing=None))
    pk = parts.placket(g, P)
    parts_.append(solidify(pk, .002, facing=None))
    tree = parts.bvh(pk.pos, pk.tri)
    for k in range(P.get('buttons', 2)):
        z = P['placket_top'] - .022 - k * P.get('button_gap', .045)
        r = tree.ray_cast(parts.Vector((0, -1.0, z)), parts.Vector((0, 1, 0)))
        if r[0] is None: continue
        parts_.append(parts.button(np.array(r[0]) - np.array(r[1]) * 0.0 + np.array([0, -.0021, 0]), -np.array([0, 1., 0]) * -1 * -1 if False else np.array(r[1])))
    for q in parts_: shell.merge(q)
    return {'Kit_Top': shell}


def polo_cuts(body, x, tri, side, P):
    """hem, sleeve hems and neckline as clean isolines. Returns x, tri, side and per-vertex hem fields (metres from each border)."""
    zs, ys = torso_axis(body)
    yc = lambda z: np.interp(z, zs, ys)
    # hem: straight around, a touch longer at the back (tennis tail), side vents handled later
    th = np.arctan2(x[:, 0], -(x[:, 1] - yc(x[:, 2])))      # 0 = front, +-pi = back
    back = (1 - np.cos(th)) * .5
    f = x[:, 2] - (P['z_hem'] - P.get('back_drop', .015) * back)
    x, tri, (side,) = G.cut_keep(x, tri, f, (side,))
    # sleeves: plane across the arm at t = L (smoothed into the torso so the isoline never touches the armscye)
    for sd in (-1, 1):
        sh, ax = arm_frame(body, sd)
        t = (x - sh) @ ax
        g = np.where(np.abs(side - sd) < .5, P['sleeve'] - t, .06)
        g = G.smooth_field(g, tri, 8, fixed=np.abs(side - sd) < .5)
        x, tri, (side,) = G.cut_keep(x, tri, g, (side,))
    # neckline: around the neck base, dipping at the front
    th = np.arctan2(x[:, 0], -(x[:, 1] - yc(x[:, 2])))
    zn = P['z_neck_front'] + (P['z_neck_back'] - P['z_neck_front']) * (1 - np.cos(th)) * .5
    r = np.hypot(x[:, 0], x[:, 1] - yc(x[:, 2]))
    f = (zn - x[:, 2]) + 2.0 * np.maximum(0, r - P['neck_r'])
    x, tri, (side,) = G.cut_keep(x, tri, f, (side,))
    r = G.largest_component(x, tri, side)
    return r[0], r[1], r[2]




def build_tank(body, P):
    """racerback V-neck tank: torso shell, armholes + neck cut as one smooth field, contrast binding all round the top, rib hem."""
    from garment import Garment, solidify, PIQUE, RIB
    x, tri, side = polo(body, P)
    g = Garment(x, tri, fabric=PIQUE, roles=('Kit_Shirt',), side=side, W=body._last_W)
    th, r = torso_theta(body, g.pos)
    g.A['f_hem'] = g.pos[:, 2] - (P['z_hem'] - P.get('back_drop', .0) * (1 - np.cos(th)) * .5)
    g.cut(g.A['f_hem'])
    g.largest()
    th, r = torso_theta(body, g.pos)
    z = g.pos[:, 2]; ax_ = np.abs(g.pos[:, 0])
    wb = (1 - np.cos(th)) * .5                                  # 0 front .. 1 back
    xf = np.interp(z, P['front_z'], P['front_x'])
    xb = np.interp(z, P['back_z'], P['back_x'])
    xl = xf * (1 - wb) + xb * wb
    f_arm = G.smax(xl - ax_, P['z_arm_floor'] - z, .02)        # keep inside the strap line, or below the armhole floor
    # front V and back U necklines; outside the strap's inner edge nothing is cut, so each strap runs on over the shoulder
    zf = P['v_bottom'] + ax_ * P['v_slope']
    xi = P['back_strap_x']
    zb = np.where(ax_ < xi, P['z_back_neck'] + (ax_ / xi) ** 2 * (P['strap_top'] - P['z_back_neck']), 2.0)
    zv = zf * (1 - wb) + zb * wb
    zv = np.where((wb > .25) & (wb < .75) & (ax_ > xi * .9), 2.0, zv)
    f_neck = zv - z
    g.A['f_top'] = G.smin(f_arm, f_neck, .006)
    g.cut(g.A['f_top'])
    g.largest()
    from garment import fill_small_holes
    fill_small_holes(g); fill_small_holes(g)
    loops = G.boundary_loops(g.tri)
    hem_loop = min(loops, key=lambda l: g.pos[l, 2].mean())
    loop_len = lambda l: np.linalg.norm(g.pos[l] - g.pos[np.roll(l, -1)], axis=1).sum()
    top_loops = [l for l in loops if l is not hem_loop and loop_len(l) > .15]
    d = G.edge_distance(g.pos, g.tri, top_loops)
    g.A['d_edge'] = d
    bw = P['binding']
    for v in [bw, bw - .0012]:
        g.insert(g.A['d_edge'] - v)
    d = g.A['d_edge']
    bind = d < bw - 1e-4
    g.A['trim'][bind] = 1
    g.A['lift'][bind] = .0008 * np.clip((bw - d[bind]) / .0012, 0, 1)
    g.F['fabric'][(g.A['d_edge'][g.tri].mean(1) < bw)] = RIB
    hb = P.get('hem_band', .025)
    for v in [hb, hb - .0012]:
        g.insert(g.A['f_hem'] - v)
    hm = g.A['f_hem'] < hb - 1e-4
    g.A['lift'][hm] = np.maximum(g.A['lift'][hm], .0007)
    g.F['fabric'][(g.A['f_hem'][g.tri].mean(1) < hb)] = RIB
    g.F['proj'][:] = 0
    from garment import smooth_borders
    smooth_borders(g)
    shell = solidify(g, P.get('thickness', .002), facing=.03)
    return {'Kit_Top': shell}
