"""POLO v2: a polo shirt built as a product, not from the body surface.
1. a low-poly cage (front panel, back panel, two sleeves, shoulder roof, neckline) placed from a few body MEASUREMENTS (shoulder points, neck
   base, torso depth, arm direction) plus the shirt's own ease, subdivided (Catmull-Clark) into a smooth garment;
2. construction as separate geometry: collar (stand + roll + fall with pointed ends), placket with buttons, rib cuffs, turned hem, side vents;
3. thickness (2.5 mm, rounded rims) and per-panel UVs (seams at the side, shoulder and armhole lines, like a sewing pattern).
Body mesh space: metres, Z up, hero faces -Y, hero's right = +X.   Blender -b --factory-startup --python polo2.py -- <hero> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector

FRONT, BACK, SLEEVE_R, SLEEVE_L, COLLAR, PLACKET, CUFF, BUTTON, HEMBAND = range(9)
PANEL_NAMES = ['front', 'back', 'sleeve_r', 'sleeve_l', 'collar', 'placket', 'cuff', 'button', 'hem']


# ------------------------------------------------------------------ design (male tennis polo, sized to MatchHero_Male)
def design_male():
    D = dict(
        yc=-.040,                       # torso centre line (front/back), measured
        n=2.6,                          # cross-section squareness
        rings=[                         # name, z (front), z (back), half width a, half depth b
            ("H", .860, .852, .192, .148),
            ('L', .980, .980, .192, .148),
            ('W', 1.100, 1.100, .199, .153),
            ('A', 1.190, 1.190, .211, .156),
        ],
        M=dict(z=1.292, a=.214, b=.150),
        S=dict(z_front=1.338, z_side=1.382, z_back=1.366, a=.240, b_front=.128, b_back=.112),
        N=dict(z_front=1.372, z_side=1.420, z_back=1.424, a=.094, b_front=.086, b_back=.072, yc=-.010),
        sleeve=dict(t=(.050, .112, .178), r=(.086, .076, .068)),
        arm_joint=np.array([.198, -.041, 1.325]),
        shoulder_pt=np.array([.240, -.040, 1.382]),
        arm_dir=np.array([.634, .013, -.773]),
        thickness=.0025,
        collar=dict(stand=.026, fall=.050, point=.040, gap=.012, thick=.0028, splay=.30),
        placket=dict(len=.092, w=.030, buttons=2),
        cuff=dict(w=.024), hem=dict(w=.026), vent=dict(h=.040, w=.006),
    )
    return D


def design_golf_male():
    """GolfKitHero_Male: royal-blue golf polo (plain collar, no tipping, 3 buttons, plain sleeve hems), hem at the belt (worn tucked)."""
    D = design_male()
    D.update(rings=[("H", 1.005, .997, .192, .148), ('L', 1.060, 1.060, .195, .150), ('W', 1.130, 1.130, .201, .154), ('A', 1.195, 1.195, .211, .156)],
             sleeve=dict(t=(.050, .115, .182), r=(.088, .080, .073)), collar=dict(stand=.026, fall=.054, point=.044, gap=.012, thick=.0028, splay=.32),
             placket=dict(len=.118, w=.030, buttons=3), cuff=dict(w=.020), vent=dict(h=.001, w=.001), waist_z=1.08, plain=True)
    return D


def design_golf_female():
    """GolfKitHero_Female: royal-blue fitted golf polo, 3 buttons, short plain sleeves, hem at the skort waist."""
    D = dict(
        yc=-.0215, n=2.5,
        rings=[("H", 1.015, 1.010, .131, .106), ('L', 1.055, 1.055, .122, .104), ('W', 1.115, 1.115, .123, .106), ('A', 1.190, 1.190, .142, .113)],
        M=dict(z=1.272, a=.147, b=.112),
        S=dict(z_front=1.318, z_side=1.352, z_back=1.338, a=.160, b_front=.094, b_back=.096),
        N=dict(z_front=1.372, z_side=1.408, z_back=1.414, a=.072, b_front=.068, b_back=.062, yc=.001),
        sleeve=dict(t=(.034, .074, .108), r=(.062, .056, .051)),
        arm_joint=np.array([.147, -.008, 1.309]), shoulder_pt=np.array([.168, -.008, 1.352]), arm_dir=np.array([.165, .094, -.98]),
        thickness=.0023, collar=dict(stand=.020, fall=.044, point=.036, gap=.010, thick=.0026, splay=.30),
        placket=dict(len=.098, w=.026, buttons=3), cuff=dict(w=.018), hem=dict(w=.024), vent=dict(h=.001, w=.001),
        armpit_x=.14, armpit_z=1.20, waist_z=1.08, plain=True, gap=.0045, tube_sleeves=True, sleeve_len=.100, sleeve_start=-.018, sleeve_r=(.054, .049), sleeve_off=(.016, .012, .0), cut_armholes=True, armhole=dict(c=(.156, .004, 1.290), s=(.046, .050, .062)),
    )
    return D


DESIGNS = {'tennis_male': design_male, 'golf_male': design_golf_male, 'golf_female': design_golf_female}


def ring_points(a, b, yc, z, n, k=16, z_back=None):
    th = np.radians(22.5 * np.arange(k))
    s, c = np.sin(th), np.cos(th)
    x = a * np.sign(s) * np.abs(s) ** (2 / n)
    y = yc - b * np.sign(c) * np.abs(c) ** (2 / n)
    zz = np.full(k, z) if z_back is None else z + (z_back - z) * (1 - c) * .5
    return np.stack([x, y, zz], 1)


def build_cage(D):
    V = []; F = []; P = []                    # verts, faces, face panel
    def add(p): V.append(np.asarray(p, float)); return len(V) - 1
    R = {}
    for name, zf, zb, a, b in D['rings']:
        R[name] = [add(p) for p in ring_points(a, b, D['yc'], zf, D['n'], z_back=zb)]
    m = D['M']; Mp = ring_points(m['a'], m['b'], D['yc'], m['z'], D['n'])
    R['M'] = [add(p) if (k not in (4, 12) or D.get('tube_sleeves')) else None for k, p in enumerate(Mp)]
    s = D['S']; th = np.radians(22.5 * np.arange(16)); sn, cs = np.sin(th), np.cos(th)
    zS = np.where(cs >= 0, s['z_front'] + (s['z_side'] - s['z_front']) * np.abs(sn) ** 1.5, s['z_back'] + (s['z_side'] - s['z_back']) * np.abs(sn) ** 1.5)
    bS = np.where(cs >= 0, s['b_front'], s['b_back'])
    xS = s['a'] * np.sign(sn) * np.abs(sn) ** (2 / D['n']); yS = D['yc'] - bS * np.sign(cs) * np.abs(cs) ** (2 / D['n'])
    R['S'] = [add((xS[k], yS[k], zS[k])) for k in range(16)]
    nn = D['N']
    zN = np.where(cs >= 0, nn['z_front'] + (nn['z_side'] - nn['z_front']) * np.abs(sn) ** 2, nn['z_back'] + (nn['z_side'] - nn['z_back']) * np.abs(sn) ** 2)
    bN = np.where(cs >= 0, nn['b_front'], nn['b_back'])
    xN = nn['a'] * np.sign(sn) * np.abs(sn) ** .8; yN = nn['yc'] - bN * np.sign(cs) * np.abs(cs) ** .8
    R['N'] = [add((xN[k], yN[k], zN[k])) for k in range(16)]
    def panel_of(k):          # torso face between vertex k and k+1 -> front (theta centre within +-90) or back
        tc = math.radians(22.5 * k + 11.25)
        return FRONT if math.cos(tc) > 0 else BACK
    order = ['H', 'L', 'W', 'A', 'M', 'S', 'N']
    hole = set() if D.get('cut_armholes') else {('A', 3), ('A', 4), ('M', 3), ('M', 4), ('A', 11), ('A', 12), ('M', 11), ('M', 12)}
    for lo, hi in zip(order[:-1], order[1:]):
        for k in range(16):
            if (lo, k) in hole: continue
            k2 = (k + 1) % 16
            a_, b_, c_, d_ = R[lo][k], R[lo][k2], R[hi][k2], R[hi][k]
            if None in (a_, b_, c_, d_): continue
            F.append((a_, b_, c_, d_)); P.append(panel_of(k))
    # sleeves: armhole loop (A3 A4 A5 M5 S5 S4 S3 M3) -> 3 rings along the arm
    for side, ks, pan in (() if D.get('tube_sleeves') else ((1, (3, 4, 5), SLEEVE_R), (-1, (13, 12, 11), SLEEVE_L))):
        k3, k4, k5 = ks
        loop = [R['A'][k3], R['A'][k4], R['A'][k5], R['M'][k5], R['S'][k5], R['S'][k4], R['S'][k3], R['M'][k3]]
        sp = D['shoulder_pt'] * np.array([side, 1, 1]); ad = D['arm_dir'] * np.array([side, 1, 1]); ad = ad / np.linalg.norm(ad)
        L = np.array([V[i] for i in loop])
        cen = L.mean(0)
        # frame round the sleeve axis: 'up' = toward the shoulder point
        up = sp - cen; up -= ad * (up @ ad); up /= np.linalg.norm(up); side_v = np.cross(ad, up)
        ang = np.arctan2((L - cen) @ side_v, (L - cen) @ up)
        sl = D['sleeve']
        prev = loop
        o = D['arm_joint'] * np.array([side, 1, 1])     # the sleeve runs along the real arm axis (from the shoulder joint)
        for t, r in zip(sl['t'], sl['r']):
            c = o + ad * t
            ring = [add(c + r * (math.cos(a) * up + math.sin(a) * side_v)) for a in ang]
            for i in range(8):
                i2 = (i + 1) % 8
                F.append((prev[i], prev[i2], ring[i2], ring[i])); P.append(pan)
            prev = ring
    return np.array(V), F, np.array(P)


def make_sleeves(D, fit, body):
    """short sleeves as tubes along the REAL arm axis (arms that hang nearly vertical cannot be lofted from the torso's armhole loop): a closed dome at the shoulder
    (hidden under the torso yoke), rings down the arm, the open hem. Layered over the torso shell; the seam is drawn by the maps."""
    objs = []
    for side in (1, -1):
        sg = np.array([side, 1, 1])
        j = (D['arm_joint'] + np.array(D.get('sleeve_shift', (0, 0, 0)))) * sg; ad = D['arm_dir'] * sg; ad = ad / np.linalg.norm(ad)
        L = D['sleeve_len']; r0, r1 = D['sleeve_r']; t0 = D.get('sleeve_start', 0.0)
        zv = np.array([0, 0, 1.0]); uu = zv - ad * (zv @ ad); uu /= np.linalg.norm(uu); vv = np.cross(ad, uu)
        N = 20
        dome = [(-.021, .30), (-.016, .62), (-.009, .86), (-.003, .97)]
        ts = [t0 + d[0] for d in dome] + list(np.linspace(t0, L, 12)); ms = [d[1] for d in dome] + [1.0] * 12
        rings = []; tval = []
        off = np.array(D.get('sleeve_off', (0, 0, 0))) * sg
        for t, m in zip(ts, ms):
            c = j + ad * t + off * float(smoothstep(0, .06, t))
            r = (r0 + (r1 - r0) * max(t - t0, 0) / (L - t0)) * m
            ph = 2 * math.pi * np.arange(N) / N + math.pi                     # i = 0 is the underside: the seam
            rings.append(np.stack([c + r * (np.cos(p_) * uu + np.sin(p_) * vv * .95) for p_ in ph]))
            tval.append(t)
        R = np.array(rings)                                                    # (nr, N, 3)
        nr = len(ts)
        tri = [t_ for jj in range(nr - 1) for i in range(N) for t_ in (((jj * N + i), (jj * N + (i + 1) % N), ((jj + 1) * N + (i + 1) % N)), ((jj * N + i), ((jj + 1) * N + (i + 1) % N), ((jj + 1) * N + i)))]
        xv, _ = fit.push_clear(body, R.reshape(-1, 3), np.array(tri), gap=D.get('gap', .0045) + .0015, passes=4, cap=.02)
        R = xv.reshape(R.shape)
        F = []; corners = []
        circ = np.array([np.linalg.norm(np.diff(np.vstack([R[jj], R[jj][:1]]), axis=0), axis=1).sum() for jj in range(nr)])
        cap_face = tuple(range(N))[::-1]
        vcoord = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(R.mean(1), axis=0), axis=1))])
        for jj in range(nr - 1):
            for i in range(N):
                i2 = (i + 1) % N
                F.append((jj * N + i, jj * N + i2, (jj + 1) * N + i2, (jj + 1) * N + i))
                corners.append([(circ[jj] * i / N, vcoord[jj]), (circ[jj] * (i + 1) / N, vcoord[jj]), (circ[jj + 1] * (i + 1) / N, vcoord[jj + 1]), (circ[jj + 1] * i / N, vcoord[jj + 1])])
        rc = circ[0] / (2 * math.pi)
        corners.append([(rc * math.cos(-2 * math.pi * i / N) + .5, rc * math.sin(-2 * math.pi * i / N) - .3) for i in range(N)])
        F.append(cap_face)
        me = bpy.data.meshes.new('Sleeve%d' % side)
        me.from_pydata([tuple(v) for v in R.reshape(-1, 3)], [], F); me.update()
        lay = me.uv_layers.new(name='UVMap'); lay.data.foreach_set('uv', np.array([c for f in corners for c in f], np.float32).ravel())
        a = me.attributes.new('panel', 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(me.polygons), SLEEVE_R if side > 0 else SLEEVE_L, np.int32))
        d_hem = np.repeat(L - np.array(tval), N)
        ang_i = np.tile(np.minimum(np.arange(N), N - np.arange(N)), nr) * (2 * math.pi / N) * (r0 * .9)
        for k_, v_ in (('d_seam', ang_i), ('d_hem', np.full(nr * N, 1.0)), ('d_neck', np.full(nr * N, 1.0)), ('d_cuff', d_hem), ('d_arm', np.full(nr * N, 1.0))):
            at = me.attributes.new(k_, 'FLOAT', 'POINT'); at.data.foreach_set('value', np.asarray(v_, np.float32))
        ob = bpy.data.objects.new('Sleeve%d' % side, me); bpy.context.scene.collection.objects.link(ob)
        bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
        f0 = bm.faces[len(bm.faces) // 2]; c0 = f0.calc_center_median(); cen = Vector(j + ad * (L / 2))
        axis_pt = cen - Vector(ad) * (Vector(ad).dot(cen - Vector(c0)))
        if f0.normal.dot(c0 - Vector(j + ad * ((c0 - Vector(j)).dot(Vector(ad))))) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free(); me.update()
        objs.append(ob)
    return objs


def make_object(name, V, F, panel):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(f) for f in F])
    me.update()
    at = me.attributes.new('panel', 'INT', 'FACE'); at.data.foreach_set('value', np.asarray(panel, np.int32))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def apply_modifier(ob, mod):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def orient_outward(ob, centre_fn):
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # recalc works for closed-ish shells; check against the torso axis and flip if inward
    s = 0.0
    for f in bm.faces:
        c = f.calc_center_median(); s += f.normal.dot(c - Vector(centre_fn(np.array(c))))
    if s < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free(); me.update()


def mesh_arrays(ob):
    me = ob.data
    n = len(me.vertices); pos = np.zeros(n * 3); me.vertices.foreach_get('co', pos); pos = pos.reshape(-1, 3)
    me.calc_loop_triangles()
    tri = np.zeros(len(me.loop_triangles) * 3, np.int32); me.loop_triangles.foreach_get('vertices', tri); tri = tri.reshape(-1, 3)
    tp = np.zeros(len(me.loop_triangles), np.int32); me.loop_triangles.foreach_get('polygon_index', tp)
    return pos, tri, tp



# ------------------------------------------------------------------ helpers on numpy meshes

def polys(ob):
    me = ob.data
    n = len(me.vertices); pos = np.zeros(n * 3); me.vertices.foreach_get('co', pos)
    loops = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', loops)
    st = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_get('loop_start', st)
    tot = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_get('loop_total', tot)
    return pos.reshape(-1, 3), [loops[a:a + b] for a, b in zip(st, tot)]


def boundary_loops_poly(faces):
    """directed boundary loops of a polygon mesh (list of vertex index arrays)."""
    cnt = {}; dirn = {}
    for f in faces:
        for i in range(len(f)):
            a, b = int(f[i]), int(f[(i + 1) % len(f)]); k = (min(a, b), max(a, b))
            cnt[k] = cnt.get(k, 0) + 1; dirn[k] = (a, b)
    nxt = {dirn[k][0]: dirn[k][1] for k, c in cnt.items() if c == 1}
    loops, seen = [], set()
    for s in list(nxt):
        if s in seen: continue
        l = [s]; seen.add(s); c = nxt[s]
        while c != s and c in nxt and c not in seen: l.append(c); seen.add(c); c = nxt[c]
        loops.append(np.array(l))
    return loops


def dist_to_polyline(x, pts, closed=True):
    q = np.concatenate([pts, pts[:1]]) if closed else pts
    a = q[:-1]; b = q[1:]; ab = b - a; L2 = (ab * ab).sum(1) + 1e-12
    out = np.full(len(x), np.inf)
    for i0 in range(0, len(x), 3000):
        xx = x[i0:i0 + 3000]
        t = np.clip(np.einsum('nsk,sk->ns', xx[:, None, :] - a[None], ab) / L2[None], 0, 1)
        d = np.linalg.norm(xx[:, None, :] - (a[None] + t[:, :, None] * ab[None]), axis=2)
        out[i0:i0 + 3000] = d.min(1)
    return out


def dist_to_segments(x, a, b):
    ab = b - a; L2 = (ab * ab).sum(1) + 1e-12
    out = np.full(len(x), np.inf)
    for i0 in range(0, len(x), 2000):
        xx = x[i0:i0 + 2000]
        t = np.clip(np.einsum('nsk,sk->ns', xx[:, None, :] - a[None], ab) / L2[None], 0, 1)
        out[i0:i0 + 2000] = np.linalg.norm(xx[:, None, :] - (a[None] + t[:, :, None] * ab[None]), axis=2).min(1)
    return out


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def vnormals(pos, faces):
    n = np.zeros_like(pos)
    for f in faces:
        p = pos[f]; c = np.cross(p[1] - p[0], p[2] - p[0]) + (np.cross(p[2] - p[0], p[3] - p[0]) if len(f) > 3 else 0)
        n[f] += c
    return n / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-12)


def new_object(name, V, F, uv=None, fattr=None, vattr=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(int(i) for i in f) for f in F]); me.update()
    if uv is not None:      # uv: per vertex (n,2) -> loops
        layer = me.uv_layers.new(name='UVMap')
        li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
        layer.data.foreach_set('uv', np.asarray(uv, np.float32)[li].ravel())
    for k, v in (fattr or {}).items():
        a = me.attributes.new(k, 'INT', 'FACE'); a.data.foreach_set('value', np.asarray(v, np.int32))
    for k, v in (vattr or {}).items():
        a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(v, np.float32))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def set_vattr(ob, name, vals):
    me = ob.data
    a = me.attributes.get(name) or me.attributes.new(name, 'FLOAT', 'POINT')
    a.data.foreach_set('value', np.asarray(vals, np.float32))


def get_vattr(ob, name):
    a = ob.data.attributes[name]; v = np.zeros(len(a.data), np.float32); a.data.foreach_get('value', v); return v


def grid_faces(nu, nv, closed_u=False):
    F = []
    for i in range(nu if closed_u else nu - 1):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            F.append((i * nv + j, i2 * nv + j, i2 * nv + j + 1, i * nv + j + 1))
    return F


# ------------------------------------------------------------------ construction parts

def collar(D, shell_tree, body, neck_loop_pts, ncen):
    """stand rising round the neck, rolled fold, fall lying back over the stand and the shoulders; open at the front for the placket,
    pointed ends. Returns V, F, uv, vertex fields (edge = distance from the fall's free edge, trim)."""
    C = D['collar']
    P = neck_loop_pts
    ang = np.arctan2(P[:, 0] - ncen[0], -(P[:, 1] - ncen[1]))         # 0 = centre front
    order = np.argsort(ang); P = P[order]; ang = ang[order]
    keep = np.abs(ang) > 1e-3
    # parameter by arc length, open at the front between -gap..+gap (x)
    q = np.concatenate([P, P[:1]]); seg = np.linalg.norm(np.diff(q, axis=0), axis=1); s = np.concatenate([[0], np.cumsum(seg)])
    L = s[-1]
    i_f = np.argmin(np.abs(ang))
    u = (s[:-1] - s[i_f]) % L
    gap = C['gap']
    # resample the open curve from u=gap (hero's right, x>0 side first) round the back to u=L-gap
    N = 72
    ut = np.linspace(gap, L - gap, N)
    o = np.argsort(u); uu = u[o]; PP = P[o]
    uu = np.concatenate([uu - L, uu, uu + L]); PP3 = np.concatenate([PP, PP, PP])
    base = np.stack([np.interp(ut, uu, PP3[:, k]) for k in range(3)], 1)
    t = ut / L                                                           # 0..1 round the neck
    up = np.array([0, 0, 1.])
    rad = base - np.array([ncen[0], ncen[1], 0]); rad[:, 2] = 0; rad /= np.linalg.norm(rad, axis=1, keepdims=True)
    back = .5 - .5 * np.cos(2 * np.pi * t)                                # 1 at the back, 0 at the front ends
    hs = C['stand'] * (.82 + .18 * back)
    end = np.clip(1 - np.minimum(t, 1 - t) / .14, 0, 1) ** 1.6           # 1 at the collar points
    wf = C['fall'] * (.9 + .1 * back) + C['point'] * end
    rows = []; prof = []
    th = C['thick']; rf = th * 1.15
    for v in (-.006, .0, .5, 1.0):                                       # stand (starts tucked 6 mm inside the neckline)
        rows.append(base + up * (hs * max(v, 0) + min(v, 0))[:, None] - rad * (.004 * max(v, 0)))
    top = rows[-1]
    for k in range(1, 6):                                                # rolled fold
        a = np.pi * k / 6
        rows.append(top + rad * (rf * (1 - np.cos(a))) + up * (rf * .7 * np.sin(a)))
    fo = rows[-1]
    # the fall: down outside the stand, then out over the shirt; its free edge sits on the shirt
    nf = 7
    for k in range(1, nf + 1):
        w = wf * k / nf
        down = np.minimum(w, hs + rf)
        outw = np.maximum(0, w - (hs + rf))
        p = fo - up * down[:, None] + rad * (.0015 + outw * .9)[:, None] - up * (outw * .42)[:, None]
        # collar points: toward the ends the fall splays away from the centre front (a triangular point, not a square end)
        tang = np.gradient(base, axis=0); tang /= np.linalg.norm(tang, axis=1, keepdims=True)
        away = np.where((t < .5)[:, None], tang, -tang)
        p += away * (C.get('splay', 0) * w * end)[:, None]
        rows.append(p)
    R = np.stack(rows, 1)                                                 # (N, nr, 3)
    nr = R.shape[1]
    # keep the stand off the neck, and the fall on top of the shirt (>= collar thickness + 1 mm above it)
    flat = R.reshape(-1, 3)
    import fit
    d, n = fit.signed(body, flat)
    flat = flat + n * np.clip(.004 - d, 0, None)[:, None]
    R = flat.reshape(N, nr, 3)
    for j in range(9, nr):
        for i in range(N):
            p = R[i, j]
            r = shell_tree.find_nearest(Vector(p))
            if r[0] is None: continue
            c = np.array(r[0]); nn = np.array(r[1])
            dd = (p - c) @ nn
            want = th + .0012
            if dd < want: R[i, j] = c + nn * want
    V = R.reshape(-1, 3)
    F = grid_faces(N, nr)
    prof_len = np.concatenate([np.zeros((N, 1)), np.cumsum(np.linalg.norm(np.diff(R, axis=1), axis=2), axis=1)], 1)
    edge = (prof_len[:, -1:] - prof_len).reshape(-1)
    arc = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(base, axis=0), axis=1))])
    uv = np.stack([np.repeat(arc, nr), prof_len.reshape(-1)], 1)
    return V, F, uv, edge


def placket(D, shell_tree, z_top):
    C = D['placket']; w = C['w']; Lp = C['len']
    xs = np.linspace(-w / 2, w / 2, 7); zs = np.linspace(z_top - Lp, z_top, 26)
    V = []
    for z in zs:
        for x in xs:
            r = shell_tree.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
            V.append(np.array(r[0]) + np.array(r[1]) * .0012 if r[0] is not None else np.array([x, -.2, z]))
    V = np.array(V)
    F = grid_faces(len(zs), len(xs))
    uv = np.stack([np.tile(xs, len(zs)), np.repeat(zs - zs[0], len(xs))], 1)
    return V, F, uv, np.tile(xs, len(zs)), np.repeat(zs - zs[0], len(xs))


def button(c, n, r=.0058, h=.0024, k=20):
    n = n / np.linalg.norm(n); a = np.cross(n, [0, 0, 1.]); a /= np.linalg.norm(a); b = np.cross(n, a)
    prof = [(0, h * .45), (.35, h * .40), (.62, h * .55), (.86, h * .75), (1.0, h * .45), (1.0, 0)]
    V = []
    for rr, hh in prof:
        for i in range(k):
            ph = 2 * np.pi * i / k
            V.append(c + (a * np.cos(ph) + b * np.sin(ph)) * r * rr + n * hh)
    V = np.array(V)
    F = [tuple(range(k))[::-1]]
    for j in range(len(prof) - 1):
        for i in range(k):
            i2 = (i + 1) % k
            F.append((j * k + i, j * k + i2, (j + 1) * k + i2, (j + 1) * k + i))
    uv = np.stack([(V - c) @ a, (V - c) @ b], 1)
    return V, F, uv


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    hero_id = argv[0]; outp = argv[1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    from mathutils.bvhtree import BVHTree
    D = DESIGNS[argv[2] if len(argv) > 2 else 'tennis_male']()
    V, F, P = build_cage(D)
    cage = make_object('Polo_cage', V, F, P)
    orient_outward(cage, lambda c: np.array([0, D['yc'], c[2]]))
    # pattern seams on the cage: panel boundaries + the underarm line of each sleeve; unwrap the cage, subdivision carries the UVs
    bm = bmesh.new(); bm.from_mesh(cage.data)
    pl = bm.faces.layers.int.get('panel')
    for e in bm.edges:
        fs = e.link_faces
        if len(fs) == 2 and fs[0][pl] != fs[1][pl]: e.seam = True
    sp = [V[i] for i in range(len(V))]
    for side in (1, -1):
        sh = D['shoulder_pt'] * np.array([side, 1, 1])
        sleeve_faces = [f for f in bm.faces if f[pl] == (SLEEVE_R if side > 0 else SLEEVE_L)]
        sv = {v for f in sleeve_faces for v in f.verts}
        for e in bm.edges:
            if all(v in sv for v in e.verts):
                # underarm line: both ends far below the shoulder point (lowest vertex of each sleeve ring)
                pass
        rings = {}
        for v in sv:
            rings.setdefault(round(((np.array(v.co) - sh) @ (D['arm_dir'] * np.array([side, 1, 1]))), 3), []).append(v)
        low = [min(vs, key=lambda v: v.co.z) for vs in rings.values()]
        lowset = set(low)
        for e in bm.edges:
            if all(v in lowset for v in e.verts): e.seam = True
    bm.to_mesh(cage.data); bm.free()
    bpy.context.view_layer.objects.active = cage; cage.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.unwrap(method='ANGLE_BASED', margin=.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    sub = cage.modifiers.new('sub', 'SUBSURF'); sub.levels = 3; sub.render_levels = 3; sub.uv_smooth = 'PRESERVE_BOUNDARIES'
    shirt = cage.copy(); shirt.data = cage.data.copy(); shirt.name = 'Polo'; bpy.context.scene.collection.objects.link(shirt)
    cage.modifiers.clear(); cage.hide_render = True; cage.name = 'Polo_cage'
    apply_modifier(shirt, shirt.modifiers['sub'])
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    pos, faces = polys(shirt)
    tri = np.array([t for f in faces for t in ([f[0], f[1], f[2]], [f[0], f[2], f[3]])])
    # worn over whatever bottom the hero has on: the hem must clear it too (the V2 shorts when they are built, else the shorts in the dump)
    others = []
    kbp = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity', 'Kit_Bottom_final.npz')
    kpos = ktri = None
    if os.path.exists(kbp) and 'v2' in np.load(kbp).files:
        kg = np.load(kbp); kpos = kg['pos']; ktri = kg['tri'][kg['part'] == 0]
        fc_ = kpos[ktri].mean(1); outer_ = ktri[fc_[:, 2] > .80]
        print('POLO obstacle: V2 shorts, %d outer faces' % len(outer_))
    elif 'Kit_Bottom' in hero.kit:
        kb = hero.kit['Kit_Bottom']; kpos = kb.pos
        fn_ = G.face_normals(kb.pos, kb.tri); fc_ = kb.pos[kb.tri].mean(1)
        radial = fc_ - np.stack([np.zeros(len(fc_)), np.full(len(fc_), D['yc']), fc_[:, 2]], 1)
        outer_ = kb.tri[(np.einsum('ij,ij->i', fn_, radial) > 0) & (fc_[:, 2] > .80)]      # its outer wall round the hips
    if kpos is not None:
        others.append((fit.MeshSurface(kpos, outer_), .004, (pos[:, 2] < 1.02).astype(float)))
        # the drawcord hangs in front of the shorts: the polo front stands a little further off there
        fc_mask = ((pos[:, 2] < 1.0) & (np.abs(pos[:, 0]) < .07) & (pos[:, 1] < D['yc'])).astype(float)
        others.append((fit.MeshSurface(kpos, outer_), .013, fc_mask))
    x, d = fit.push_clear_multi(body, pos, tri, gap=D.get('gap', .006), others=others)
    mv_ = np.linalg.norm(x - pos, axis=1); im_ = int(np.argmax(mv_)); print('FIT worst move at', pos[im_].round(3), '->', x[im_].round(3))
    print('FIT moved %d verts, max %.1f mm, min clearance %.1f mm' % (int((np.linalg.norm(x - pos, axis=1) > 1e-4).sum()), np.linalg.norm(x - pos, axis=1).max() * 1000, d.min() * 1000))
    pos = x
    if D.get('cut_armholes'):
        # armholes cut by smooth ellipsoids (a bit smaller than the sleeve tubes that cover their edge): kill faces, snap the boundary onto the curve
        def arm_f(P):
            out = np.full(len(P), 9.0); c = D['armhole']['c']; sa = D['armhole']['s']
            for sd in (1, -1):
                d_ = (P - np.array([sd * c[0], c[1], c[2]])) / np.array(sa)
                on = (np.sign(P[:, 0]) == sd) | (np.abs(P[:, 0]) < .02)
                out = np.minimum(out, np.where(on, (np.sqrt((d_ * d_).sum(1)) - 1.0) * min(sa) * .85, 9.0))
            return out
        shirt.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shirt.data.update()
        f_ = arm_f(pos)
        bm = bmesh.new(); bm.from_mesh(shirt.data); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
        kill = [fc for fc in bm.faces if np.mean([f_[v.index] for v in fc.verts]) < 0]
        bmesh.ops.delete(bm, geom=kill, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(shirt.data); bm.free()
        pos, faces = polys(shirt)
        lps = boundary_loops_poly(faces)
        hem0 = min(lps, key=lambda l: pos[l, 2].mean()); neck0 = max(lps, key=lambda l: pos[l, 2].mean())
        oth = [l for l in lps if l is not hem0 and l is not neck0]
        if oth:
            bnd = np.unique(np.concatenate(oth)); nrm0 = vnormals(pos, faces)
            for it in range(5):
                fb = arm_f(pos[bnd]); eps = 1e-4; g = np.zeros((len(bnd), 3))
                for a_ in range(3):
                    dp = np.zeros(3); dp[a_] = eps; g[:, a_] = (arm_f(pos[bnd] + dp) - fb) / eps
                nb_ = nrm0[bnd]; g = g - (g * nb_).sum(1, keepdims=True) * nb_
                step = np.clip((fb / ((g * g).sum(1) + 1e-7))[:, None] * g, -.004, .004); pos[bnd] = pos[bnd] - step
        shirt.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shirt.data.update()
        print('ARMHOLES cut: %d faces removed' % len(kill))
    panel = np.zeros(len(faces), np.int32); shirt.data.attributes['panel'].data.foreach_get('value', panel)
    loops = boundary_loops_poly(faces)
    hem = min(loops, key=lambda l: pos[l, 2].mean()); neck = max(loops, key=lambda l: pos[l, 2].mean())
    cuffs = [l for l in loops if l is not hem and l is not neck]
    armholes = []
    if D.get('tube_sleeves'): armholes, cuffs = cuffs, []
    # seam distance (any panel boundary), hem / cuff / neck distances
    # seam lines = edges shared by two different panels (side, shoulder and armhole seams); distance to those segments
    ef = {}
    for fi, f in enumerate(faces):
        for i in range(len(f)):
            a_, b_ = int(f[i]), int(f[(i + 1) % len(f)]); ef.setdefault((min(a_, b_), max(a_, b_)), []).append(panel[fi])
    segs = np.array([k for k, v in ef.items() if len(v) == 2 and v[0] != v[1]])
    d_seam = dist_to_segments(pos, pos[segs[:, 0]], pos[segs[:, 1]]) if len(segs) else np.full(len(pos), 1.0)
    d_hem = dist_to_polyline(pos, pos[hem]); d_neck = dist_to_polyline(pos, pos[neck])
    d_cuff = np.minimum(*[dist_to_polyline(pos, pos[c]) for c in cuffs]) if len(cuffs) > 1 else (dist_to_polyline(pos, pos[cuffs[0]]) if cuffs else np.full(len(pos), 1.0))
    d_arm = np.minimum.reduce([dist_to_polyline(pos, pos[l]) for l in armholes]) if armholes else np.full(len(pos), 1.0)
    # ---- folds: soft drape at the waist sides and under the arms, very subtle vertical drape lower down
    n = vnormals(pos, faces)
    disp = np.zeros(len(pos))
    yc = D['yc']; thv = np.arctan2(pos[:, 0], -(pos[:, 1] - yc)); side = np.abs(np.sin(thv))
    torso = np.isin(np.arange(len(pos)), np.unique(np.concatenate([f for f, p in zip(faces, panel) if p in (FRONT, BACK)])))
    z = pos[:, 2]
    # waist: a few soft diagonal folds gathered toward each side seam (where a straight shirt meets the narrower waist)
    for sd in (1, -1):
        sideness = np.clip(np.sin(thv) * sd, 0, 1) ** 4
        env = torso * sideness * np.exp(-((z - D.get('waist_z', 1.03)) / .06) ** 2)
        phase = (z - .95) / .045 * 2 * np.pi + (np.cos(thv) * 3.0) * sd
        disp += .0022 * env * np.sin(phase)
    # underarms: drag folds radiating from each armpit into the chest and back
    for sd in (1, -1):
        ap = np.array([sd * D.get('armpit_x', .20), yc, D.get('armpit_z', 1.19)])
        dd = pos - ap; r_ = np.linalg.norm(dd, axis=1)
        env = np.exp(-(r_ / .065) ** 2) * torso * smoothstep(.0, .015, r_)
        ang = np.arctan2(dd[:, 2], np.abs(dd[:, 1]) + 1e-6)
        disp += .0016 * env * np.sin(ang * 4 + r_ * 40)
    # a hint of vertical drape below the chest (front and back panels hang straight)
    drape = torso * smoothstep(1.14, 1.0, z) * (1 - side) ** 1.5
    disp += .0007 * drape * np.sin(thv * 7 + 1.3)
    # turned hem: the 2.6 cm band stands 0.9 mm proud with a soft ledge
    disp += .0009 * (1 - smoothstep(D['hem']['w'] - .0015, D['hem']['w'] + .0005, d_hem)) * torso
    pos = pos + n * disp[:, None]
    shirt.data.vertices.foreach_set('co', pos.astype(np.float32).ravel()); shirt.data.update()
    for k, v in (('d_seam', d_seam), ('d_hem', d_hem), ('d_neck', d_neck), ('d_cuff', d_cuff), ('d_arm', d_arm)):
        set_vattr(shirt, k, v)
    # ---- side vents: a narrow open slit up each side seam from the hem
    # (handled as a cut on the shell: remove faces inside the slit)
    vh, vw = D['vent']['h'], D['vent']['w']
    bm = bmesh.new(); bm.from_mesh(shirt.data); bm.verts.ensure_lookup_table()
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        th_ = math.atan2(c.x, -(c.y - yc))
        dside = min(abs(th_ - math.pi / 2), abs(th_ + math.pi / 2)) * .19
        dh = np.mean([d_hem[v.index] for v in f.verts])
        if dh < vh and dside < vw * (1 - dh / vh) * .5 + .0004: kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES'); bm.to_mesh(shirt.data); bm.free()
    shell_tree = BVHTree.FromObject(shirt, bpy.context.evaluated_depsgraph_get())
    # ---- collar
    pos2, faces2 = polys(shirt)
    neck_pts = pos2[[v for v in neck if v < len(pos2)]]
    ncen = np.array([0, D['N']['yc'], 0])
    cV, cF, cuv, cedge = collar(D, shell_tree, body, neck_pts, ncen)
    print('COLLAR bounds', cV.min(0).round(3), cV.max(0).round(3), 'neck pts', len(neck_pts), neck_pts.min(0).round(3), neck_pts.max(0).round(3))
    col = new_object('Collar', cV, cF, uv=cuv * 3, fattr={'panel': np.full(len(cF), COLLAR)}, vattr={'collar_edge': cedge})
    # ---- placket + buttons
    ztop = pos2[neck][:, 2][np.argmin(np.abs(pos2[neck][:, 0]) + (pos2[neck][:, 1] > yc) * 9)]
    pV, pF, puv, pkx, pkz = placket(D, shell_tree, ztop)
    pk = new_object('Placket', pV, pF, uv=puv * 3, fattr={'panel': np.full(len(pF), PLACKET)}, vattr={'pk_x': pkx, 'pk_z': pkz})
    pk_tree = BVHTree.FromObject(pk, bpy.context.evaluated_depsgraph_get())
    bts = []
    for k in range(D['placket']['buttons']):
        zb = ztop - .024 - k * .044
        r = pk_tree.ray_cast(Vector((0, -1.0, zb)), Vector((0, 1, 0)))
        if r[0] is None: continue
        nb_ = np.array(r[1]); nb_ = nb_ if nb_[1] < 0 else -nb_          # out of the shirt (toward -y)
        bV, bF, buv = button(np.array(r[0]) + nb_ * .0021, nb_)              # on top of the placket (its 2 mm thickness)
        bts.append(new_object('Button%d' % k, bV, bF, uv=buv * 3, fattr={'panel': np.full(len(bF), BUTTON)}))
    # ---- rib cuffs: the last 2.4 cm of each sleeve becomes a separate rib band, sitting 1 mm proud
    bm = bmesh.new(); bm.from_mesh(shirt.data)
    dc = bm.verts.layers.float.get('d_cuff')
    cut = [f for f in bm.faces if all(v[dc] < D['cuff']['w'] for v in f.verts)]
    cuffbm = bmesh.new()
    vmap = {}
    uvl = bm.loops.layers.uv.active
    for f in cut:
        vs = []
        for v in f.verts:
            if v.index not in vmap: vmap[v.index] = cuffbm.verts.new(v.co + v.normal * .001)
            vs.append(vmap[v.index])
        cuffbm.faces.new(vs)
    cuffme = bpy.data.meshes.new('Cuffs'); cuffbm.to_mesh(cuffme); cuffbm.free()
    cuffs_ob = bpy.data.objects.new('Cuffs', cuffme); bpy.context.scene.collection.objects.link(cuffs_ob)
    # its own UVs: round the arm x along the arm
    cp_ = np.zeros(len(cuffme.vertices) * 3); cuffme.vertices.foreach_get('co', cp_); cp_ = cp_.reshape(-1, 3)
    layer = cuffme.uv_layers.new(name='UVMap'); li = np.zeros(len(cuffme.loops), np.int32); cuffme.loops.foreach_get('vertex_index', li)
    cu = np.zeros((len(cp_), 2))
    for sd in (1, -1):
        m = np.sign(cp_[:, 0]) == sd
        o = D['arm_joint'] * np.array([sd, 1, 1]); ad = D['arm_dir'] * np.array([sd, 1, 1]); ad /= np.linalg.norm(ad)
        upv = np.array([0, 0, 1.]) - ad * ad[2]; upv /= np.linalg.norm(upv); sv = np.cross(ad, upv)
        dd = cp_[m] - o
        cu[m, 0] = np.arctan2(dd @ sv, dd @ upv) * .075 + (2 if sd > 0 else 3)
        cu[m, 1] = dd @ ad
    layer.data.foreach_set('uv', cu.astype(np.float32)[li].ravel())
    a = cuffme.attributes.new('panel', 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(cuffme.polygons), CUFF, np.int32))
    dcv = np.array([cut[0].verts[0][dc]]) if False else None
    bmesh.ops.delete(bm, geom=cut, context='FACES'); bm.to_mesh(shirt.data); bm.free()
    # cuff fields: distance from the sleeve end (rebuilt from positions)
    if cuffs: set_vattr(cuffs_ob, 'd_cuff', dist_to_polyline(cp_, pos2[np.concatenate(cuffs)], closed=False))
    # ---- one garment object (Kit_Top), smooth, real thickness with rounded edges
    parts = [shirt, col, pk, cuffs_ob] + bts + (make_sleeves(D, fit, body) if D.get('tube_sleeves') else [])
    thick = {shirt.name: D['thickness'], col.name: D['collar']['thick'], pk.name: .002, cuffs_ob.name: .003, 'Sleeve1': D['thickness'], 'Sleeve-1': D['thickness']}
    for o in parts:
        o.data.shade_smooth()
        if o.name.startswith('Button'):
            o.data.materials.append(bpy.data.materials.get('outer') or bpy.data.materials.new('outer')); continue
        # faces: material 0 = outer cloth, 1 = inner wall, 2 = rim (Solidify material offsets)
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = thick.get(o.name, .0025); sm.offset = -1; sm.use_even_offset = True
        sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
        if o.name.startswith('Sleeve'): sm.use_even_offset = False; sm.nonmanifold_thickness_mode = 'CONSTRAINTS'
        apply_modifier(o, sm)
        if o is shirt:
            # the inside of the shirt is only ever seen through its openings: keep the inner wall 6 cm in from the hem, neck, sleeves and vents
            me_ = o.data
            fl = {k: get_vattr(o, k) for k in ('d_hem', 'd_neck', 'd_cuff', 'd_arm')}
            farv = np.minimum(np.minimum(np.minimum(fl['d_hem'], fl['d_neck']), fl['d_cuff'] - D['cuff']['w']), fl['d_arm']) > .06
            bm = bmesh.new(); bm.from_mesh(me_); bm.verts.ensure_lookup_table()
            drop = [f for f in bm.faces if f.material_index == 1 and all(farv[v.index] for v in f.verts)]
            bmesh.ops.delete(bm, geom=drop, context='FACES'); bm.to_mesh(me_); bm.free()
        bv = o.modifiers.new('bevel', 'BEVEL'); bv.width = min(.0011, thick.get(o.name, .0025) * .45); bv.segments = 2; bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(55); bv.harden_normals = False
        apply_modifier(o, bv)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = shirt
    bpy.ops.object.join()
    shirt.name = 'Kit_Top'
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('POLO parts: shell %d faces, collar %d, placket %d, buttons %d, cuffs %d' % (len(shirt.data.polygons), len(cF), len(pF), len(bts), len(cuffme.polygons)))


if __name__ == '__main__' or 'polo2.py' in ' '.join(sys.argv):
    main()
