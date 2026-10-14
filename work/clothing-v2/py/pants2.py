"""PANTS v2: shorts / trousers built as a product (not from the body surface).
Same idea as polo2.py: a low-poly cage from a few DESIGN numbers (rings of the pelvis, a crotch seam, two straight leg tubes), Catmull-Clark x3,
grain-aligned panel UVs, construction as separate geometry (waistband, drawcord + eyelets, belt loops, hem, side slits), thickness with
rounded rims. The body only decides where the cloth has to be pushed out to clear the skin (fit.push_clear_multi).
Body mesh space: metres, Z up, hero faces -Y, hero's right = +X.
Blender -b --factory-startup --python pants2.py -- <hero> <design> <out.blend>        design: male_shorts | male_trousers | ..."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
import polo2 as P2                    # shared helpers (new_object, polys, boundary loops, distance fields, apply_modifier ...)
from polo2 import new_object, polys, boundary_loops_poly, dist_to_polyline, dist_to_segments, smoothstep, vnormals, set_vattr, get_vattr, apply_modifier, grid_faces

FR, BR, BL, FL = range(4)          # panels: front right, back right, back left, front left (hero's right = +X)
PANEL_NAMES = ['front_r', 'back_r', 'back_l', 'front_l']
# part kinds (face attribute 'kind' = maps.KIND ids): 0 shell, 12 band, 4 cord, 14 buckle (eyelets), 5 loop
KIND = {'shell': 0, 'band': 12, 'cord': 4, 'eyelet': 14, 'loop': 5, 'tip': 15}
KIND_LOOP = 5
ROLE_SHORTS, ROLE_BAND, ROLE_HW = 0, 1, 2          # submesh roles: Kit_Shorts, Kit_ShortsBand, Kit_GolfHardware / Kit_Buckle
FABRIC = dict(twill=6, rib=9, mesh=2, rubber=3)


# ------------------------------------------------------------------ designs (metres, body mesh space)
def design_male_shorts():
    """MatchHero_Male tennis shorts: navy tailored woven, flat front, drawcord, slanted front pockets, back pocket, side hem slits, hem mid-thigh,
    straight cut (the leg tube barely tapers)."""
    return dict(
        name='male_shorts', n_pel=2.5, n_leg=2.3,
        # pelvis rings top -> crotch: z, half width a, half depth b, centre y
        pelvis=[(.892, .186, .150, -.038), (.858, .190, .152, -.038), (.818, .198, .157, -.043), (.780, .207, .153, -.047), (.745, .214, .147, -.048)],
        crotch_dip=.028,
        # leg rings below the crotch: z, centre x, a, centre y, b, blend toward the ellipse (0 = still the half-pelvis D shape)
        legs=[(.700, .114, .104, -.044, .124, .50), (.650, .121, .100, -.044, .119, .85), (.612, .126, .097, -.044, .115, 1.0), (.582, .129, .095, -.044, .112, 1.0)],
        thick=.0030, band=dict(h=.044, thick=.0036, lift=.0012), hem_w=.026, slit=dict(h=.040, w=.006),
        cord=dict(r=.0021, spread=.034, drop=.100, eyelet_r=.0042), eyelets=True, belt_loops=0,
        pocket=dict(top=(.078, .846), bot=(.175, .742)),              # slanted front pocket line: (distance from centre front along the ring, z) start / end
        back_pocket=dict(z=.800, x0=.040, x1=.150),
        fit_gap=.0065, roles=('Kit_Shorts', 'Kit_ShortsBand', 'Kit_Buckle'),
    )


def design_female_undershorts():
    """MatchHero_Female tight navy under-shorts worn under the pleated skirt (spandex-like knit): hug the hips and thighs, hem mid-thigh."""
    return dict(
        name='female_undershorts', n_pel=2.3, n_leg=2.1,
        pelvis=[(.985, .130, .100, -.021), (.945, .148, .109, -.009), (.905, .158, .113, .000), (.865, .169, .1145, .004), (.825, .178, .110, .004), (.785, .185, .100, -.003), (.752, .186, .092, -.007)],
        crotch_dip=.012,
        legs=[(.715, .093, .089, -.009, .088, .50), (.690, .093, .085, -.006, .087, .90), (.668, .093, .081, -.004, .086, 1.0), (.648, .093, .078, -.003, .085, 1.0)],
        thick=.0022, band=dict(h=.022, thick=.0026, lift=.0010), hem_w=.016, slit=dict(h=0.0, w=.006),
        cord=dict(r=.002, spread=.03, drop=.1, eyelet_r=.004), eyelets=False, belt_loops=0, pocket=None, back_pocket=None,
        fit_gap=.0024, roles=('Kit_ShortsBand', 'Kit_ShortsBand', 'Kit_Buckle'), shell_kind=18, fabric=7, subdiv=2, shell_role=1, no_arms=True,
    )


def design_golf_male_trousers():
    """GolfKitHero_Male flat-front trousers: navy woven, mid-rise, belt loops, slant front pockets, welt back pockets, pressed creases, straight leg tapering slightly,
    hem 8 cm above the ground (a small break over the shoe)."""
    zs = [.66, .56, .46, .36, .27, .18, .118]                       # hem 12.5 cm above the sole plane: it rests on the instep with a small break instead of flaring round the shoe
    cx = [.110 + .080 * (.75 - z) / .66 for z in zs]; cy = [-.044 + .041 * (.75 - z) / .66 for z in zs]
    a = [.106, .099, .089, .077, .067, .060, .057]; b = [.128, .120, .108, .095, .085, .078, .075]
    t = [.55, .9, 1, 1, 1, 1, 1]
    return dict(
        name='golf_male_trousers', n_pel=2.5, n_leg=2.2,
        pelvis=[(.990, .178, .134, -.054), (.955, .180, .137, -.050), (.915, .184, .142, -.043), (.875, .192, .147, -.038), (.835, .202, .152, -.040), (.795, .209, .140, -.045), (.752, .214, .128, -.045)],
        crotch_dip=.026, legs=[(z, x, aa, y, bb, tt) for z, x, aa, y, bb, tt in zip(zs, cx, a, cy, b, t)],
        thick=.0028, band=dict(h=.036, thick=.0034, lift=.0010), hem_w=.020, slit=dict(h=0.0, w=.006),
        cord=dict(r=.002, spread=.03, drop=.1, eyelet_r=.004), eyelets=False, loops=dict(n=5, angles=(-34, 34, -112, 112, 180)),
        pocket=dict(top=(.085, .940), bot=(.178, .830)), back_pocket=dict(z=.905, x0=.035, x1=.120),
        fit_gap=.0065, roles=('Kit_Shorts', 'Kit_ShortsBand', 'Kit_Buckle'), band_role=0, crease=True, subdiv=2, hem_slant=.026,
    )


DESIGNS = {'male_shorts': design_male_shorts, 'female_undershorts': design_female_undershorts, 'golf_male_trousers': design_golf_male_trousers}


# ------------------------------------------------------------------ cage
def ring_pts(a, b, yc, z, n, k=16):
    th = np.radians(360.0 / k * np.arange(k))
    s, c = np.sin(th), np.cos(th)
    return np.stack([a * np.sign(s) * np.abs(s) ** (2 / n), yc - b * np.sign(c) * np.abs(c) ** (2 / n), np.full(k, z)], 1)


def ellipse_pt(a, b, cx, cy, z, n, phi, side):
    s, c = math.sin(phi), math.cos(phi)
    x = a * np.sign(s) * abs(s) ** (2 / n); y = -b * np.sign(c) * abs(c) ** (2 / n)
    return np.array([side * (cx + x), cy + y, z])


def build_cage(D):
    """returns V (n,3), F (quads), panel per face, per-corner uv (m) per face, info dict"""
    V = []
    def add(p): V.append(np.asarray(p, float)); return len(V) - 1
    pel = []                                                      # pel[r][k] vertex ids
    for (z, a, b, yc) in D['pelvis']:
        pel.append([add(p) for p in ring_pts(a, b, yc, z, D['n_pel'])])
    nr = len(pel); C = pel[-1]
    # crotch seam chain: back centre (k=8) -> front centre (k=0), three vertices dipping below the crotch ring
    yb, yf = V[C[8]][1], V[C[0]][1]; zc = V[C[0]][2]
    chain = []
    for f in (.25, .5, .75):
        chain.append(add((0.0, yb + f * (yf - yb), zc - D['crotch_dip'] * math.sin(math.pi * f) ** .7)))
    # leg rings: per side, 12 verts: outer arc j0..j8 (front centre -> side -> back centre), then inner i1 (back), i2 (inseam), i3 (front)
    legs = {1: [], -1: []}
    for side in (1, -1):
        top = [C[j] if side > 0 else C[(16 - j) % 16] for j in range(9)] + chain
        legs[side].append(top)
        nl = len(D['legs'])
        for li, (z, cx, a, cy, b, t) in enumerate(D['legs']):
            ring = []
            wsl = D.get('hem_slant', 0.0) * (1.0 if li == nl - 1 else (.35 if li == nl - 2 else 0.0))      # slanted hem: the back drapes lower than the front
            def slant(p_): p_ = np.array(p_, float); p_[2] -= wsl * (p_[1] - cy) / b; return p_
            for j in range(9):
                kk = j if side > 0 else (16 - j) % 16
                d = np.array([V[C[kk]][0], V[C[kk]][1], z])
                e = ellipse_pt(a, b, cx, cy, z, D['n_leg'], math.radians(22.5 * j), side)
                ring.append(add(slant((1 - t) * d + t * e)))
            for q, phi in enumerate((225, 270, 315)):
                ci = chain[q]
                d = np.array([0.0, V[ci][1], z])
                e = ellipse_pt(a, b, cx, cy, z, D['n_leg'], math.radians(phi), side)
                ring.append(add(slant((1 - t) * d + t * e)))
            legs[side].append(ring)
    V = np.array(V)
    F = []; panel = []; corner = []
    # ---- pelvis rows
    def ring_s(ids, closed_half=True):
        pts = V[ids]; seg = np.linalg.norm(np.diff(pts, axis=0), axis=1); return np.concatenate([[0], np.cumsum(seg)])
    pel_s = []                                                     # arc length from the front centre, the short way round
    for r in range(nr):
        ids = pel[r] + [pel[r][0]]; cum = ring_s(ids)             # cum[k] forward distance to k, cum[16] = total
        s = np.array([cum[k] if k <= 8 else cum[16] - cum[k] for k in range(16)]); pel_s.append(s)
    pel_v = [np.zeros(16)]
    for r in range(1, nr):
        pel_v.append(pel_v[-1] + np.linalg.norm(V[pel[r]] - V[pel[r - 1]], axis=1))
    uvmap = {}                                                     # (vertex id, panel) -> (u, v)
    for r in range(nr):
        for k in range(16):
            for pn in ((FR, BR) if k <= 8 else ()) + ((BL, FL) if k >= 8 or k == 0 else ()):
                uvmap[(pel[r][k], pn)] = (pel_s[r][k], pel_v[r][k])
    for r in range(nr - 1):
        for k in range(16):
            k2 = (k + 1) % 16
            pn = FR if k <= 3 else BR if k <= 7 else BL if k <= 11 else FL
            F.append((pel[r][k], pel[r][k2], pel[r + 1][k2], pel[r + 1][k])); panel.append(pn)
    vC = pel_v[-1].mean()
    # ---- legs
    for side in (1, -1):
        rings = legs[side]
        pf, pb = (FR, BR) if side > 0 else (FL, BL)
        # per ring arc coordinates (front-dir, back-dir) and v
        sF = []; sB = []; vv = []
        for m, ring in enumerate(rings):
            pts = V[ring]; seg = np.linalg.norm(np.diff(np.vstack([pts, pts[:1]]), axis=0), axis=1)     # seg[p] = |p -> p+1|
            cum = np.concatenate([[0], np.cumsum(seg[:8])])                                             # j0..j8
            sf = np.zeros(12); sb = np.zeros(12)
            sf[:9] = cum; sb[:9] = cum
            sb[9] = cum[8] + seg[8]; sb[10] = sb[9] + seg[9]                                           # back side: j8 -> i1 -> i2
            sf[11] = -seg[11]; sf[10] = sf[11] - seg[10]                                               # front side: j0 <- i3 <- i2
            sF.append(sf); sB.append(sb)
            if m == 0: vv.append(np.full(12, vC))
            else: vv.append(vv[-1] + np.linalg.norm(pts - V[rings[m - 1]], axis=1))
        for m, ring in enumerate(rings):
            for p in range(12):
                for pn, ss in ((pf, sF[m]), (pb, sB[m])):
                    uvmap.setdefault((ring[p], pn), (ss[p], vv[m][p]))
        for m in range(len(rings) - 1):
            for p in range(12):
                p2 = (p + 1) % 12
                F.append((rings[m][p], rings[m][p2], rings[m + 1][p2], rings[m + 1][p])); panel.append(pf if (p <= 3 or p >= 10) else pb)
    corner = [[uvmap[(v, pn)] for v in f] for f, pn in zip(F, panel)]
    return V, F, np.array(panel), corner, dict(pel=pel, chain=chain, legs=legs)


def make_cage_object(name, V, F, panel, corner):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(f) for f in F]); me.update()
    at = me.attributes.new('panel', 'INT', 'FACE'); at.data.foreach_set('value', np.asarray(panel, np.int32))
    layer = me.uv_layers.new(name='UVMap')
    uv = np.array([c for f in corner for c in f], np.float32)
    layer.data.foreach_set('uv', uv.ravel())
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def orient_faces(ob, centre_fn):
    """flip each face whose normal points toward its local axis (centre_fn(point) -> axis point)."""
    me = ob.data; bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active
    flip = []
    for f in bm.faces:
        c = f.calc_center_median(); a = Vector(centre_fn(np.array(c)))
        if f.normal.dot(c - a) < 0: flip.append(f)
    # reversing a face reverses its loop order; per-loop uv follows its loop automatically
    bmesh.ops.reverse_faces(bm, faces=flip)
    bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
    return len(flip)


def leg_axis_fn(D):
    """per face: for the pelvis (above the crotch) the torso axis, below it the axis of the nearer leg."""
    zc = D['pelvis'][-1][0]
    legc = {s: np.array([s * D['legs'][1][1], D['legs'][1][3]]) for s in (1, -1)}
    yc = np.mean([p[3] for p in D['pelvis']])
    def f(c):
        if c[2] > zc - .002: return np.array([0, yc, c[2]])
        s = 1 if c[0] >= 0 else -1
        return np.array([legc[s][0], legc[s][1], c[2]])
    return f


# ------------------------------------------------------------------ fields / construction parts
def surface_point(tree, o, d, far=1.5):
    r = tree.ray_cast(Vector(o), Vector(d), far)
    return (np.array(r[0]), np.array(r[1])) if r[0] is not None else (None, None)


def tube(path, r, k=8, cap=True):
    """closed round tube along a polyline (n,3); returns V, F, per-vertex arc length."""
    path = np.asarray(path, float); n = len(path)
    t = np.gradient(path, axis=0); t /= np.linalg.norm(t, axis=1, keepdims=True) + 1e-12
    ref = np.array([0, 0, 1.]); V = []
    prev = None
    for i in range(n):
        a = np.cross(t[i], ref if abs(t[i] @ ref) < .95 else np.array([1., 0, 0])); a /= np.linalg.norm(a)
        b = np.cross(t[i], a)
        rr = r[i] if hasattr(r, '__len__') else r
        for q in range(k):
            ph = 2 * math.pi * q / k
            V.append(path[i] + (a * math.cos(ph) + b * math.sin(ph)) * rr)
    F = []
    for i in range(n - 1):
        for q in range(k):
            q2 = (q + 1) % k
            F.append((i * k + q, i * k + q2, (i + 1) * k + q2, (i + 1) * k + q))
    if cap:
        F.append(tuple(range(k))[::-1]); F.append(tuple(range((n - 1) * k, n * k)))
    arc = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))])
    return np.array(V), F, np.repeat(arc, k)


def build(hero_id, design, outp):
    D = DESIGNS[design]()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    from mathutils.bvhtree import BVHTree
    V, F, panel, corner, info = build_cage(D)
    cage = make_cage_object('Pants_cage', V, F, panel, corner)
    nflip = orient_faces(cage, leg_axis_fn(D))
    print('CAGE verts %d faces %d (flipped %d)' % (len(V), len(F), nflip))
    sub = cage.modifiers.new('sub', 'SUBSURF'); sub.levels = D.get('subdiv', 3); sub.render_levels = D.get('subdiv', 3); sub.uv_smooth = 'PRESERVE_BOUNDARIES'
    shell = cage.copy(); shell.data = cage.data.copy(); shell.name = 'Pants'; bpy.context.scene.collection.objects.link(shell)
    cage.modifiers.clear(); cage.hide_render = True
    apply_modifier(shell, shell.modifiers['sub'])
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    if D.get('no_arms'): body = G.body_no_arms(body)
    pos, faces = polys(shell)
    tri = np.array([t for f in faces for t in ([f[0], f[1], f[2]], [f[0], f[2], f[3]])])
    x, d = fit.push_clear_multi(body, pos, tri, gap=D['fit_gap'])
    mv = np.linalg.norm(x - pos, axis=1)
    print('FIT moved %d verts, max %.1f mm, min clearance %.1f mm' % (int((mv > 1e-4).sum()), mv.max() * 1000, d.min() * 1000))
    pos = x
    pan = np.zeros(len(faces), np.int32); shell.data.attributes['panel'].data.foreach_get('value', pan)
    loops = boundary_loops_poly(faces)
    print('LOOPS', len(loops), [(len(l), round(float(pos[l, 2].mean()), 3)) for l in loops])
    top = max(loops, key=lambda l: pos[l, 2].mean())
    hems = [l for l in loops if l is not top]
    # seam lines (panel boundaries), hem and waist distance fields
    ef = {}
    for fi, f in enumerate(faces):
        for i in range(len(f)):
            a_, b_ = int(f[i]), int(f[(i + 1) % len(f)]); ef.setdefault((min(a_, b_), max(a_, b_)), []).append(pan[fi])
    segs = np.array([k for k, v in ef.items() if len(v) == 2 and v[0] != v[1]])
    d_seam = dist_to_segments(pos, pos[segs[:, 0]], pos[segs[:, 1]])
    d_top = dist_to_polyline(pos, pos[top]); d_hem = np.minimum(*[dist_to_polyline(pos, pos[h]) for h in hems])
    n = vnormals(pos, faces)
    # ---- soft drape: gathered pleats under the waistband, a few diagonal pulls at the crotch / seat, nothing at the hem
    yc = -.042
    thv = np.arctan2(pos[:, 0], -(pos[:, 1] - yc)); z = pos[:, 2]
    disp = np.zeros(len(pos))
    disp += .0010 * np.exp(-((d_top - .052) / .014) ** 2) * np.sin(thv * 30 + 1.1)                     # elastic gather just under the band
    crot = np.exp(-(np.linalg.norm(pos - np.array([0, -.09, .735]), axis=1) / .075) ** 2)
    disp += .0018 * crot * np.sin(np.arctan2(pos[:, 2] - .735, pos[:, 0] + 1e-6) * 5 + 2.0)           # pulls radiating from the crotch
    seat = np.exp(-(np.linalg.norm(pos - np.array([0, .06, .78]), axis=1) / .08) ** 2)
    disp += .0012 * seat * np.sin(z * 90 + thv * 3)
    disp += .0009 * (1 - smoothstep(D['hem_w'] - .0015, D['hem_w'] + .0005, d_hem))                    # turned hem 0.9 mm proud
    pos = pos + n * disp[:, None]
    shell.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shell.data.update()
    for k, v in (('d_seam', d_seam), ('d_hem', d_hem), ('d_top', d_top)): set_vattr(shell, k, v)
    # ---- surface-bound fields: pocket lines and back pocket welts as distance to 3D polylines lying on the shell
    shell_tree = BVHTree.FromObject(shell, bpy.context.evaluated_depsgraph_get())
    def surf_line(pts_cyl, n=14):
        """pts_cyl: [(theta (rad, 0 = front centre, + = hero's right), z)] -> 3D polyline on the shell"""
        th = np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(pts_cyl)), [p[0] for p in pts_cyl])
        zz = np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(pts_cyl)), [p[1] for p in pts_cyl])
        out = []
        for t_, z_ in zip(th, zz):
            o = np.array([0, yc, z_]); dv = np.array([math.sin(t_), -math.cos(t_), 0])
            r = tree_ray(shell_tree, o + dv * .02, dv)
            if r is not None: out.append(r)
        return np.array(out)
    def tree_ray(tr, o, dv):
        r = tr.ray_cast(Vector(o), Vector(dv), 1.0)
        far = None; oo = np.array(o)
        for _ in range(6):                         # outermost hit
            if r[0] is None: break
            far = np.array(r[0]); oo = far + dv * 1e-4; r = tr.ray_cast(Vector(oo), Vector(dv), 1.0)
        return far
    def near_hit(tr, o):
        r = tr.ray_cast(Vector(o), Vector((0, 1., 0)), 2.0)
        return np.array(r[0]) if r[0] is not None else None
    fields = {}
    if D.get('crease'):
        zs_l = np.array([r[0] for r in D['legs']]); cx_l = np.array([r[1] for r in D['legs']])
        zz = pos[:, 2]; cxz = np.interp(zz, zs_l[::-1], cx_l[::-1])
        dcr = np.where(zz < D['pelvis'][-1][0], np.abs(np.abs(pos[:, 0]) - cxz), 1.0)
        # extend the crease up the front of the trousers (from the crotch to the waistband) along the same line
        zt_ = D['pelvis'][-1][0]; cx_top = cx_l[0] + (cx_l[0] - cx_l[1]) * (zt_ - zs_l[0]) / (zs_l[0] - zs_l[1])
        up_ = zz >= zt_; dcr = np.where(up_, np.abs(np.abs(pos[:, 0]) - cx_top * .9), dcr)
        fields['d_crease'] = dcr
    pk = D['pocket']
    for side, nm in ((1, 'r'), (-1, 'l')):
        if not pk: break
        a0 = math.asin(min(.99, pk['top'][0] / .19)); a1 = math.asin(min(.99, pk['bot'][0] / .20))
        line = surf_line([(side * a0, pk['top'][1]), (side * a1, pk['bot'][1])], 16)
        if len(line) > 2: fields['d_pocket_' + nm] = dist_to_polyline(pos, line, closed=False)
    bp = D['back_pocket']
    if bp:
        t0 = math.pi - math.asin(bp['x1'] / .21); t1 = math.pi - math.asin(bp['x0'] / .21)           # right-hand back pocket: x > 0
        bl_ = []
        for dz in (0.0, -.012):
            ln = surf_line([(t0, bp['z'] + dz), (t1, bp['z'] + dz)], 10)
            if len(ln) > 2: bl_.append(dist_to_polyline(pos, ln, closed=False))
        if bl_: fields['d_bpocket'] = np.minimum(*bl_) if len(bl_) > 1 else bl_[0]
    for k, v in fields.items(): set_vattr(shell, k, v)
    # ---- side hem slits (cut out of the shell) up each side seam
    sh, sw = D['slit']['h'], D['slit']['w']
    bm = bmesh.new(); bm.from_mesh(shell.data); bm.verts.ensure_lookup_table()
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        side_ = c.x
        # the side seam of each leg is the outermost line (j4): distance from the leg's outer-most x at that height
        dh = np.mean([d_hem[v.index] for v in f.verts])
        if dh < sh and c.z < .70:
            dd = abs(np.mean([d_seam[v.index] for v in f.verts]))
            # a face is in the slit if it lies within the half width of a seam edge that is a side seam (outer x)
            if dd < sw * (1 - dh / sh) * .5 + .0004 and abs(c.x) > .205: kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES'); bm.to_mesh(shell.data); bm.free()
    shell_tree = BVHTree.FromObject(shell, bpy.context.evaluated_depsgraph_get())
    # ---- waistband: the top band of the shell becomes its own rib-like band, lifted 1.2 mm, with its own thickness
    bm = bmesh.new(); bm.from_mesh(shell.data); bm.verts.ensure_lookup_table()
    dt = bm.verts.layers.float.get('d_top')
    bw = D['band']['h']
    cut = [f for f in bm.faces if all(v[dt] < bw for v in f.verts)]
    bb = bmesh.new(); vmap = {}
    for f in cut:
        vs = []
        for v in f.verts:
            if v.index not in vmap: vmap[v.index] = bb.verts.new(v.co + v.normal * D['band']['lift'])
            vs.append(vmap[v.index])
        bb.faces.new(vs)
    band_me = bpy.data.meshes.new('Waistband'); bb.to_mesh(band_me); bb.free()
    band = bpy.data.objects.new('Waistband', band_me); bpy.context.scene.collection.objects.link(band)
    bp_ = np.zeros(len(band_me.vertices) * 3); band_me.vertices.foreach_get('co', bp_); bp_ = bp_.reshape(-1, 3)
    # band UVs per corner: along the ring (arc by theta * r) x height; corners of a face are unwrapped to within pi of its first corner so the
    # back-centre seam becomes a texture seam instead of a face smeared across the map
    thb = np.arctan2(bp_[:, 0], -(bp_[:, 1] - yc))
    layer = band_me.uv_layers.new(name='UVMap')
    li = np.zeros(len(band_me.loops), np.int32); band_me.loops.foreach_get('vertex_index', li)
    lst = np.zeros(len(band_me.polygons), np.int32); band_me.polygons.foreach_get('loop_start', lst)
    lto = np.zeros(len(band_me.polygons), np.int32); band_me.polygons.foreach_get('loop_total', lto)
    cu = np.zeros((len(li), 2))
    for s0, t0 in zip(lst, lto):
        th_ = thb[li[s0:s0 + t0]].copy()
        th_ = th_[0] + (np.angle(np.exp(1j * (th_ - th_[0]))))
        cu[s0:s0 + t0, 0] = th_ * .19; cu[s0:s0 + t0, 1] = bp_[li[s0:s0 + t0], 2]
    layer.data.foreach_set('uv', cu.astype(np.float32).ravel())
    set_vattr(band, 'd_top', dist_to_polyline(bp_, pos[top], closed=True))
    set_vattr(band, 'band_z', bp_[:, 2] - (pos[top][:, 2].mean() - bw))
    for nm, vals in (('face_kind', KIND['band']), ('fabric', D.get('band_fabric', FABRIC['twill'])), ('role', D.get('band_role', ROLE_BAND)), ('panel', 4)):
        a = band_me.attributes.new(nm, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(band_me.polygons), vals, np.int32))
    bmesh.ops.delete(bm, geom=cut, context='FACES'); bm.to_mesh(shell.data); bm.free()
    # ---- eyelets + drawcord (front): two eyelet rings in the band, two cord strands out of them, crossing at a knot, ends hanging with aglets
    parts = [shell, band]
    if D.get('eyelets'):
        ctop = float(pos[top][:, 2].mean()); zc_e = ctop - bw * .52
        bandtree = BVHTree.FromObject(band, bpy.context.evaluated_depsgraph_get())
        cs = D['cord']
        ends = []
        for side in (1, -1):
            xe = side * cs['spread'] * .5 * .82
            p, nrm = surface_point(bandtree, (xe, -.5, zc_e), (0, 1, 0))
            if p is None: continue
            nrm = nrm if nrm[1] < 0 else -nrm
            ends.append((p, nrm))
            # eyelet: a short ring (torus-like: tube round a circle) set into the band
            ang = np.linspace(0, 2 * math.pi, 14, endpoint=False)
            a_ = np.cross(nrm, [0, 0, 1.]); a_ /= np.linalg.norm(a_); b_ = np.cross(nrm, a_)
            circ = [p + nrm * .0008 + (a_ * math.cos(t) + b_ * math.sin(t)) * cs['eyelet_r'] for t in ang]
            circ.append(circ[0]); circ.append(circ[1])
            ev, ef_, _ = tube(circ, .0011, k=6, cap=False)
            eo = new_object('Eyelet', ev, ef_, uv=ev[:, :2] * 40, fattr={'panel': np.full(len(ef_), 5), 'face_kind': np.full(len(ef_), KIND['eyelet']),
                                                                         'fabric': np.full(len(ef_), 0), 'role': np.full(len(ef_), ROLE_HW)})
            parts.append(eo)
        if len(ends) == 2:
            # strands: from each eyelet the cord comes out, loops under the opposite eyelet's side to the knot at the centre front
            (pL, nL), (pR, nR) = (ends[1], ends[0]) if ends[0][0][0] > ends[1][0][0] else ends
            kz = zc_e - .004; knot = np.array([0.0, 0, kz])
            r_ = near_hit(bandtree, (0, -.5, kz))
            kn = r_ if r_ is not None else knot
            hang = []
            for sgn, pe, ne in ((1, pR, nR), (-1, pL, nL)):
                pts = []
                # from the eyelet, over the band surface to the knot, then down the front of the shorts
                for t in np.linspace(0, 1, 8):
                    q = pe * (1 - t) + kn * t
                    rr = near_hit(bandtree, (q[0], -.5, q[2]))
                    q = (rr if rr is not None else q) + np.array([0, -1, 0]) * (cs['r'] + .0006)
                    pts.append(q)
                # tail: 10 cm down, slightly outward, each side, following the shorts' front
                for dz in np.linspace(.012, cs['drop'], 12):
                    xx = sgn * (.006 + .008 * (dz / cs['drop']) ** .8)
                    zz = kn[2] - dz
                    rr = near_hit(shell_tree, (xx, -.5, zz))
                    if rr is None: rr = np.array([xx, kn[1], zz])
                    pts.append(rr + np.array([0, -1, 0]) * (cs['r'] + .0010 + .003 * (dz / cs['drop'])))
                pts = np.array(pts)
                tv, tf, _ = tube(pts, cs['r'], k=8)
                to = new_object('Cord', tv, tf, uv=np.stack([np.repeat(np.arange(len(pts)) * .01, 8), np.tile(np.arange(8) * .004, len(pts))], 1),
                                fattr={'panel': np.full(len(tf), 6), 'face_kind': np.full(len(tf), KIND['cord']), 'fabric': np.full(len(tf), FABRIC['twill']), 'role': np.full(len(tf), ROLE_BAND)})
                parts.append(to)
                # aglet: a short, slightly thicker end tube
                ag = np.array([pts[-1] + (pts[-1] - pts[-3]) / np.linalg.norm(pts[-1] - pts[-3]) * s_ for s_ in np.linspace(-.014, .004, 4)])
                av, af, _ = tube(ag, cs['r'] * 1.35, k=8)
                ao = new_object('Aglet', av, af, uv=np.stack([av[:, 0] * 30, av[:, 2] * 30], 1),
                                fattr={'panel': np.full(len(af), 7), 'face_kind': np.full(len(af), KIND['tip']), 'fabric': np.full(len(af), 0), 'role': np.full(len(af), ROLE_HW)})
                parts.append(ao)
    # ---- belt loops: small arched straps over the waistband, the belt passes under them
    if D.get('loops'):
        bandtree2 = BVHTree.FromObject(band, bpy.context.evaluated_depsgraph_get())
        ctop2 = float(pos[top][:, 2].mean()); zlo, zhi = ctop2 - bw - .003, ctop2 + .008
        for ang in D['loops']['angles']:
            th = math.radians(ang); dv = np.array([math.sin(th), -math.cos(th), 0.0])
            zmid = (zlo + zhi) / 2
            r = bandtree2.ray_cast(Vector((0, yc, zmid)) + Vector(dv) * .02, Vector(dv), 1.0)
            far = None; oo = np.array([0, yc, zmid]) + dv * .02
            for _ in range(5):
                if r[0] is None: break
                far = (np.array(r[0]), np.array(r[1])); oo = far[0] + dv * 1e-4; r = bandtree2.ray_cast(Vector(oo), Vector(dv), 1.0)
            if far is None: continue
            pt, nn = far; nn = nn / np.linalg.norm(nn)
            tg = np.cross(nn, [0, 0, 1.]); tg /= np.linalg.norm(tg)
            LV = []; LUV = []; nzs = 8; nws = 4
            for iz in range(nzs):
                s_ = iz / (nzs - 1)
                for iw in range(nws):
                    u_ = (iw / (nws - 1) - .5) * .0115
                    lift = .0052 * math.sin(math.pi * s_) ** .55 + .0006
                    LV.append(pt + tg * u_ + np.array([0, 0, 1.]) * (zlo + (zhi - zlo) * s_ - pt[2]) + nn * lift); LUV.append((u_, s_ * .05))
            lo = new_object('BeltLoop', np.array(LV), grid_faces(nzs, nws), uv=np.array(LUV), fattr={'panel': np.full((nzs - 1) * (nws - 1), 8), 'face_kind': np.full((nzs - 1) * (nws - 1), KIND['loop']),
                            'fabric': np.full((nzs - 1) * (nws - 1), D.get('fabric', FABRIC['twill'])), 'role': np.full((nzs - 1) * (nws - 1), ROLE_SHORTS)})
            parts.append(lo)
    # ---- shell face attributes, thickness, rounded rims
    nf = len(shell.data.polygons)
    for nm, vals in (('face_kind', D.get('shell_kind', KIND['shell'])), ('fabric', D.get('fabric', FABRIC['twill'])), ('role', D.get('shell_role', ROLE_SHORTS))):
        a = shell.data.attributes.new(nm, 'INT', 'FACE'); a.data.foreach_set('value', np.full(nf, vals, np.int32))
    thick = {shell.name: D['thick'], band.name: D['band']['thick']}
    for o in parts:
        o.data.shade_smooth()
        if o.name in ('Eyelet', 'Cord', 'Aglet') or o.name.startswith(('Eyelet', 'Cord', 'Aglet')):
            for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
            continue
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = thick.get(o.name, .003); sm.offset = -1; sm.use_even_offset = True
        sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
        apply_modifier(o, sm)
        if o is shell:
            me_ = o.data
            fl = {k: get_vattr(o, k) for k in ('d_hem', 'd_top')}
            farv = np.minimum(fl['d_hem'], fl['d_top']) > .06
            bm = bmesh.new(); bm.from_mesh(me_); bm.verts.ensure_lookup_table()
            drop = [f for f in bm.faces if f.material_index == 1 and all(farv[v.index] for v in f.verts)]
            bmesh.ops.delete(bm, geom=drop, context='FACES'); bm.to_mesh(me_); bm.free()
        bv = o.modifiers.new('bevel', 'BEVEL'); bv.width = min(.0011, thick.get(o.name, .003) * .42); bv.segments = 2; bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(55)
        apply_modifier(o, bv)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = shell
    bpy.ops.object.join()
    shell.name = 'Kit_Bottom'; shell['roles'] = list(D['roles'])
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('PANTS parts: %d faces, band %d, cord/hardware %d objects' % (nf, len(band_me.polygons), len(parts) - 2))


if __name__ == '__main__' or 'pants2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2])
