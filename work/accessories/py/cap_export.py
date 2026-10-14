"""Fit a built cap to a hero's head, write the piece (.bin + npz + manifest entry) and bake its maps.
Blender -b --factory-startup --python cap_export.py -- <tennis|golf> <Male|Female|both> [--skip-maps]
Outputs: work/accessories/out/<Sex>/<item>/ (item = tennis_cap | golf_cap)."""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_pipeline as PL, acc_fit as F, acc_canvas as CV
import cap as CAP, cap_maps as CM

OUT = os.path.join(A.ROOT, 'out')
EYE_TOP = {'Male': 1.592, 'Female': 1.565}            # top of the eye openings in the body mesh
FRONT_RIM_Z = {k: v + .044 for k, v in EYE_TOP.items()}   # front edge of the crown: a thumb above the brow, forehead and eyes stay visible


def build_piece(variant, H=None):
    parts, D, C, V = CAP.build(variant, H=H)
    CAP.finish(parts, variant, '')
    for ob in parts:
        rule = 'crown' if ob.name == 'Crown' else 'smart' if ob.name.startswith(('Piping', 'Buckle', 'Button')) else 'sheet'
        PL.unwrap_part(ob, rule)
    joined = G.join(parts, 'Kit_Cap')
    PL.pack_joined(joined)
    piece = PL.extract(joined)
    return piece, D, C, V, joined


def socket_world(hd, p):
    sx, sy, sz, pitch, dy, dz = p
    anchor_u = np.array([0.0, dz, -dy])
    return F.rt4(F.Rx(pitch), anchor_u)


def world_N(hd, p, pts):
    sx, sy, sz = p[0], p[1], p[2]
    q = F.product_to_socket(np.asarray(pts) * np.array([sx, sy, sz]))
    return F.socket_to_world_N(socket_world(hd, p), q)


def fit(hd, sex, piece, D, verbose=True):
    k, z, tri = piece['kind'], piece['zone'], piece['tri']
    def vset(mask): return np.unique(tri[mask])
    inner = vset((k == CAP.K_CROWN) & (z == 2)); band = vset((k == CAP.K_BAND) & (z == 0)); brim = vset((k == CAP.K_BRIM) & (z == 2))
    P = piece['pos']
    rng = np.random.default_rng(3)
    sub = lambda idx, n: idx if len(idx) <= n else rng.choice(idx, n, replace=False)
    inner_s, band_s, brim_s = sub(inner, 500), sub(band, 250), sub(brim, 150)
    fb = piece['fields'].get('d_edge', np.ones(len(P)))
    brim_edge_s = np.unique(tri[(k == CAP.K_BRIM) & (z == 2)]); brim_edge_s = brim_edge_s[fb[brim_edge_s] < .0012]
    head_pos, head_tri = F.region(hd, hd.rest_pos['Head'][2] - 0.07)
    clear = F.Clearance(head_pos, head_tri)
    front_pt = np.array([[0, -D['B'], 0.0]])
    target = FRONT_RIM_Z[sex]

    def soft(x):                                   # smooth hinge: ~0 for x<0, ~x for x>0
        return np.logaddexp(0, x / .0004) * .0004

    def cost(p, full=False):
        ci = clear(world_N(hd, p, P[inner_s])); cb = clear(world_N(hd, p, P[band_s])); cr = clear(world_N(hd, p, P[brim_s]))
        pen_all = soft(.0015 - np.concatenate([ci, cb]))
        c1 = 3e7 * np.sum(pen_all ** 2) / 60.0
        c2 = 1e7 * np.sum(soft(.003 - cr) ** 2) / 40.0
        c3 = ((np.mean(cb) - .0021) / .0010) ** 2
        c4 = np.mean(soft(ci - .0035)) / .0018
        zf = world_N(hd, p, front_pt)[0, 2]
        c5 = ((zf - target) / .0015) ** 2
        zmin_brim = world_N(hd, p, P[brim_edge_s])[:, 2].min()
        c7 = (max(0, EYE_TOP[sex] + .008 - zmin_brim) / .0020) ** 2
        c6 = ((p[3] + 4) / 6) ** 2 * .3 + 40 * (p[0] - p[1]) ** 2 + (max(0, p[3] + 1.5) / 1.0) ** 2 + (max(0, -9 - p[3]) / 1.0) ** 2 + ((p[2] - .88) / .22) ** 2 * .3
        return c1 + c2 + c3 + c4 + c5 + c6 + c7

    # start from the head itself: cranium depth / centre just above the target rim, the rim plane a pitch-worth above the front rim
    pitch0 = -5.0
    zb = target - D['B'] * math.sin(math.radians(-pitch0))
    m = (np.abs(head_pos[:, 2] - (target + .012)) < .006) & (np.abs(head_pos[:, 0]) < .1)
    ymin, ymax = head_pos[m, 1].min(), head_pos[m, 1].max()
    dy0 = .5 * (ymin + ymax); sy0 = (.5 * (ymax - ymin) + .006) / (D['B'] - D['T'] - .0028)
    sz0 = (head_pos[:, 2].max() + .0015 - zb) / (D['H'] - D['T'])
    best = None
    for pitch in (-8.0, -4.0, 0.0):
        zb = target - D['B'] * math.sin(math.radians(-pitch))
        x0 = np.array([1.0, sy0, sz0, pitch, dy0, zb])
        step = np.array([.03, .03, .04, 3.0, .006, .006])
        x1, f1 = F.nelder_mead(cost, x0, step, iters=140)
        x1, f1 = F.nelder_mead(cost, x1, step * .4, iters=140)
        if verbose: print('  start pitch', pitch, 'cost', round(float(f1), 2), np.round(x1, 4))
        if best is None or f1 < best[1]: best = (x1, f1)
    best, fv = best
    # full evaluation
    Pw = lambda idx: world_N(hd, best, P[idx])
    ci, cb, cr = clear(Pw(inner)), clear(Pw(band)), clear(Pw(brim))
    zf = world_N(hd, best, front_pt)[0, 2]
    rep = dict(params=[float(v) for v in best], cost=float(fv), min_inner=float(ci.min()), min_band=float(cb.min()), mean_band=float(cb.mean()),
               mean_inner=float(ci.mean()), min_brim=float(cr.min()), front_rim_z=float(zf), target_front_rim_z=target)
    if verbose: print(sex, json.dumps(rep))
    return best, rep, clear


def hug(hd, piece, Pw, clear, target=.0022, minimum=.0015, factor=.78, sigma=.008, lim=.03):
    """the cap hugs the head: smooth displacement of the crown-side vertices toward (or away from) the head so the lining sits ~2 mm off the skin.
    The head is never touched. Brim: only its root, fading with distance from the crown. Returns new positions and a report."""
    k, z, tri = piece['kind'], piece['zone'], piece['tri']
    lining = np.unique(tri[((k == CAP.K_CROWN) & (z == 2)) | ((k == CAP.K_BAND) & (z == 0))])
    c, u = clear(Pw[lining], with_dir=True)
    need = np.where(c > target, (c - target) * factor, (c - target))      # shrink only part of the slack; push out fully where it is tight
    need = np.clip(need, -lim, lim)
    disp = -u * need[:, None]                                              # u points away from the head: -u*need moves toward the head when slack
    # smooth the field over the lining (it is a property of the head, not of the mesh)
    disp = F.gaussian_transfer(Pw[lining], disp, Pw[lining], sigma)
    D_all = F.gaussian_transfer(Pw[lining], disp, Pw, sigma * 1.4)
    # who follows: everything but the brim; the brim only near its root
    w = np.ones(len(Pw))
    isb = np.zeros(len(Pw), bool); isb[np.unique(tri[k == CAP.K_BRIM])] = True
    dr = piece['fields'].get('d_root', np.zeros(len(Pw)))
    # brim vertices (split copies share their face's kind): weight by distance from the root
    kb = np.zeros(len(Pw), bool); kb[tri[k == CAP.K_BRIM].ravel()] = True
    w[kb] = np.exp(-(dr[kb] / .018) ** 2)
    Pn = Pw + D_all * w[:, None]
    c2 = clear(Pn[lining])
    return Pn, dict(min_clear=float(c2.min()), mean_clear=float(c2.mean()), max_shift=float(np.linalg.norm(D_all * w[:, None], axis=1).max()))


def relieve(hd, piece, Pw, clear, minimum=.0016, sigma=.0045):
    """pushes the cap OUT where the lining is closer than `minimum` to the skin (a small, local field; the head never moves)."""
    k, z, tri = piece['kind'], piece['zone'], piece['tri']
    lining = np.unique(tri[((k == CAP.K_CROWN) & (z == 2)) | ((k == CAP.K_BAND) & (z == 0))])
    for it in range(4):
        c, u = clear(Pw[lining], with_dir=True)
        bad = c < minimum
        if not bad.any(): break
        need = np.zeros(len(lining)); need[bad] = (minimum + .0002 - c[bad])
        disp = u * need[:, None]
        # spread only from the offending vertices, so the rest of the fit stays where it is
        w_src = Pw[lining][bad]
        D = F.gaussian_transfer(w_src, disp[bad], Pw, sigma * 1.6)
        # fade by distance to the nearest offender so far-away vertices do not move
        d_near = np.sqrt(((Pw[:, None, :] - w_src[None, ::max(1, len(w_src) // 400), :]) ** 2).sum(-1).min(1)) if len(w_src) else np.full(len(Pw), 1e9)
        fade = np.exp(-(d_near / (sigma * 4.0)) ** 2)
        Pw = Pw + D * fade[:, None]
    c = clear(Pw[lining])
    return Pw, dict(min_clear=float(c.min()), mean_clear=float(c.mean()))


def main():
    args = A.args_after_dashes()
    variant = args[0]; which = args[1] if len(args) > 1 else 'both'
    skip_maps = '--skip-maps' in args
    item = f'{variant}_cap'
    piece0, D, C, V, joined = build_piece(variant)
    print('piece', len(piece0['pos']), 'verts', len(piece0['tri']), 'tris, S=%.4f m/uv' % piece0['S'])
    if which == 'product':
        # the product itself (no hero): for the alone sheets. Maps are baked with self-occlusion only.
        out_dir = os.path.join(OUT, 'Product', item); os.makedirs(out_dir, exist_ok=True)
        P = piece0['pos']; Nn = piece0['nrm']
        n = len(P); bi = np.zeros((n, 4), np.int32); bw = np.zeros((n, 4), np.float32); bw[:, 0] = 1
        entry = PL.write_piece(out_dir, 'Kit_Cap', piece0, ['Headwear'], bi, bw, [np.eye(4)], 2048, ['Kit_Cap', 'Kit_Buckle'], detail='cap_' + variant)
        json.dump(dict(item=item, sex='Product', slot='Headwear', npz='Kit_Cap_final.npz', tag='Cap', pieces=['Kit_Cap'], piece=entry, entries={'Kit_Cap': entry}, tags={'Kit_Cap': 'Cap'}),
                  open(os.path.join(out_dir, 'piece.json'), 'w'), indent=1)
        ao = CV.vertex_ao(P, Nn, piece0['tri'], rays=12, dist=.035)
        CV.bake(os.path.join(out_dir, 'Kit_Cap_final.npz'), out_dir, 'Cap', lambda cv: CM.details_cap(cv, variant), ao=ao)
        print('wrote product', out_dir); return
    for sex in (['Male', 'Female'] if which == 'both' else [which]):
        hd = A.HeroData(sex)
        best, rep, clear = fit(hd, sex, piece0, D)
        P = world_N(hd, best, piece0['pos'])
        for it in range(3):
            P, hrep = hug(hd, piece0, P, clear, target=.0021 if it < 2 else .0019, factor=.78 if it == 0 else .5)
            print('hug', it, hrep)
        P, rrep = relieve(hd, piece0, P, clear); print('relieve', rrep); hrep['final'] = rrep
        rep['hug'] = hrep
        # normals: rotate by the linear part (scale is near-uniform; renormalised)
        N0 = piece0['nrm']
        sc = np.array(best[:3])
        Nn = F.recompute_normals(P, piece0['tri'], piece0['src'])
        piece = dict(piece0); piece['pos'] = P; piece['nrm'] = Nn
        W = socket_world(hd, best)
        out_dir = os.path.join(OUT, sex, item); os.makedirs(out_dir, exist_ok=True)
        n = len(P)
        bi = np.zeros((n, 4), np.int32); bw = np.zeros((n, 4), np.float32); bw[:, 0] = 1
        bind = [F.bindpose_for(hd, W)]
        entry = PL.write_piece(out_dir, 'Kit_Cap', piece, ['Headwear'], bi, bw, bind, 2048, ['Kit_Cap', 'Kit_Buckle'], detail='cap_' + variant)
        entry['name'] = item
        pos_l, quat, scl = F.local_pose_under(hd, 'Head', W)
        meta = dict(item=item, sex=sex, slot='Headwear', npz='Kit_Cap_final.npz', tag='Cap', pieces=['Kit_Cap'], parent_bone='Head', socket='Headwear', socket_local_position=pos_l, socket_local_rotation=quat,
                    fit=dict(scale=[float(v) for v in best[:3]], pitch_deg=float(best[3]), anchor_N=[0.0, float(best[4]), float(best[5])]), fit_report=rep, piece=entry, entries={'Kit_Cap': entry}, tags={'Kit_Cap': 'Cap'})
        json.dump(meta, open(os.path.join(out_dir, 'piece.json'), 'w'), indent=1)
        if not skip_maps:
            # AO: against the cap itself and the hero's head
            hp, ht = F.region(hd, hd.rest_pos['Head'][2] - 0.07)
            nt = piece['tri']
            ao = CV.vertex_ao(P, Nn, nt, rays=12, dist=.035, extra=(hp, ht))
            CV.bake(os.path.join(out_dir, 'Kit_Cap_final.npz'), out_dir, 'Cap', lambda cv: CM.details_cap(cv, variant), ao=ao)
        print('wrote', out_dir)


if __name__ == '__main__':
    main()
