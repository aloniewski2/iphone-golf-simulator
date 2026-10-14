"""BAG: the golf stand/carry bag built alone as a product (Blender 5.2 python, numpy).
Blender -b --factory-startup --python bag.py -- <out.blend> [open|folded] [preview_dir]

Product frame = N frame: Z up, front (pockets) = -Y, hero's left = +X. Origin: x = 0, y = 0 at the shell centre line, z = 0 at the floor (bottom of the rubber base).
The back panel (strap side) is the plane y = YB.  Real construction: tapered superellipse shell (flat back, rounded front) with a navy boot, thick rubber base,
padded rolled rim + piping, recessed lining and 4-way divider with a floor, carry handle, raised navy front pocket with welted zip, a long vertical zip pocket,
a side zip pocket with piping outline, a back pocket and hub patch, two hinged stand legs (folded / open) on a bracket, two padded shoulder straps (webbing + foam pad +
slider buckle), three knit head covers on woods, two irons, a putter and two wedge grips.
Per-point fields feed the map baker (see bag_maps.py).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import acc_common as A
import acc_geo as G
import bag_geo as BG
from bag_geo import unit, sstep

K = dict(SHELL=0, LINING=1, BASE=2, RIM=3, PIPING=4, POCKET=5, ZIP=6, DIVIDER=7, STRAP=8, BUCKLE=9, LEG=10, BRACKET=11, SHAFT=12, IRON=13, GRIP=14,
         COVER=15, HANDLE=16, FOOT=17, STUD=18, PULL=19, HUB=20, BACKPOCKET=21, FLOOR=22, HINGE=23, POCKET2=24)
FAB = {'twill': 6, 'pique': 7, 'leather': 8, 'rib': 9, 'plastic': 4}
METAL_KINDS = {K['BUCKLE'], K['LEG'], K['BRACKET'], K['SHAFT'], K['IRON'], K['STUD'], K['PULL'], K['HINGE']}


def wrap_pi(a): return (np.asarray(a) + math.pi) % (2 * math.pi) - math.pi


# relative texel density per part (x100): the detailed front / pocket / head-cover islands get more of the map than the plain back, base, lining and the metal parts
UVW = dict(shell_front=100, shell_back=38)
UV_PART = [('Lining', 40), ('Floor', 30), ('Div', 40), ('Base', 55), ('Rim', 100), ('Pipe', 100), ('FrontPocket', 160), ('LowerPocket', 140), ('SidePocket', 140), ('BackPocket', 90),
           ('Hub', 100), ('Zip', 150), ('Pull', 80), ('Strap_', 135), ('Handle', 100), ('Cover_', 175), ('Grip_', 120), ('Shaft_', 35), ('Head_', 60), ('Leg', 35), ('Bracket', 35),
           ('Hinge', 35), ('Stud', 70), ('Shell', 100)]


def uv_weight(name):
    if 'Buckle' in name: return 90
    for pre, w in UV_PART:
        if name.startswith(pre): return w
    return 100


def set_kind(ob, kind, fabric, sub=None):
    G.set_face_attr(ob, 'kind', kind)
    G.set_face_attr(ob, 'fabric', fabric)
    G.set_face_attr(ob, 'sub', (1 if kind in METAL_KINDS else 0) if sub is None else sub)
    return ob


def xform(ob, R=None, t=None):
    P = G.positions(ob)
    if R is not None: P = P @ np.asarray(R).T
    if t is not None: P = P + np.asarray(t)
    G.set_positions(ob, P)
    return ob


# ====================================================================== the shell
class Shell:
    Z0, ZM, ZT, YB = 0.050, 0.450, 0.765, 0.103          # boot bottom, widest height, top edge of the fabric, back panel plane
    A = (0.1050, 0.1330, 0.1210)                         # semi-width at (Z0, ZM, ZT)
    B = (0.0900, 0.1125, 0.1030)                         # semi-depth
    NF, NB = 2.7, 5.0                                    # superellipse exponents: rounded front, flatter back

    def __init__(self):
        phis, s = self._table(self.ZM)
        self.L = float(s[-1] - s[0])                     # perimeter at the widest height
        self._cache = {}

    def prof(self, z, vals):
        z = np.asarray(z, float); lo, mid, hi = vals
        tl = np.clip((self.ZM - z) / (self.ZM - self.Z0), 0, 1.6); th = np.clip((z - self.ZM) / (self.ZT - self.ZM), 0, 1.6)
        return np.where(z <= self.ZM, mid + (lo - mid) * tl ** 2, mid + (hi - mid) * th ** 2)

    def expo(self, phi): return self.NF + (self.NB - self.NF) * sstep(0.05, 0.95, -np.cos(phi))

    def point(self, phi, z):
        phi, z = np.broadcast_arrays(np.asarray(phi, float), np.asarray(z, float))
        a = self.prof(z, self.A); b = self.prof(z, self.B)
        n = self.expo(phi); s_, c_ = np.sin(phi), np.cos(phi)
        x = a * np.sign(s_) * np.abs(s_) ** (2 / n)
        y = (self.YB - b) - b * np.sign(c_) * np.abs(c_) ** (2 / n)
        return np.stack([x, y, z], -1)

    def normal(self, phi, z):
        e = 1e-4
        dp = self.point(phi + e, z) - self.point(phi - e, z)
        dz = self.point(phi, z + e) - self.point(phi, z - e)
        return unit(np.cross(dp, dz))

    def hnormal(self, phi, z):
        n = self.normal(phi, z).copy(); n[..., 2] = 0
        return unit(n)

    def _table(self, z):
        phis = np.linspace(-math.pi, math.pi, 3001)
        P = self.point(phis, z)
        ds = np.linalg.norm(np.diff(P, axis=0), axis=1)
        s = np.concatenate([[0], np.cumsum(ds)]); s -= s[1500]
        return phis, s

    def table(self, z):
        k = round(float(z), 5)
        if k not in self._cache: self._cache[k] = self._table(z)
        return self._cache[k]

    def grid_phi(self, nu):
        phis, s = self.table(self.ZM)
        si = -self.L / 2 + np.arange(nu) * self.L / nu
        return np.interp(si, s, phis)

    def zb(self, phi):
        """height of the navy boot's upper seam: low at the front/back centre, climbing at the sides."""
        return 0.140 + 0.150 * np.abs(np.sin(phi)) ** 2.5

    def at(self, s, z, off=0.0):
        """point on the shell surface at horizontal arc coordinate s (0 = front centre, + toward hero's left/+X, +-L/2 = back centre) and height z, pushed `off` along the normal.
        Also sets self.last_normal / self.last_phi."""
        s, z = np.broadcast_arrays(np.asarray(s, float), np.asarray(z, float))
        off = np.broadcast_to(np.asarray(off, float), s.shape)
        s = ((s + self.L / 2) % self.L) - self.L / 2
        out = np.zeros(s.shape + (3,)); nrm = np.zeros_like(out); ph_out = np.zeros(s.shape)
        for zu in np.unique(np.round(z, 6)):
            m = np.abs(z - zu) < 1e-6
            phis, ss = self.table(zu)
            ph = np.interp(s[m], ss, phis)
            P = self.point(ph, zu); N = self.normal(ph, zu)
            out[m] = P + N * off[m][..., None]
            nrm[m] = N; ph_out[m] = ph
        self.last_normal = nrm; self.last_phi = ph_out
        return out


def face_mean(ob, field):
    me = ob.data
    v = np.zeros(len(me.vertices), np.float32); me.attributes[field].data.foreach_get('value', v)
    return np.array([v[list(p.vertices)].mean() for p in me.polygons])


def set_faces_from(ob, name, vals):
    a = ob.data.attributes.new(name, 'INT', 'FACE'); a.data.foreach_set('value', np.asarray(vals, np.int32))


def build_shell(S, nu=64, r1=5, r2=30):
    phis = S.grid_phi(nu)
    zb = S.zb(phis)
    nv = r1 + r2 + 1
    Z = np.zeros((nu, nv))
    for j in range(nv):
        Z[:, j] = S.Z0 + (zb - S.Z0) * j / r1 if j <= r1 else zb + (S.ZT - zb) * (j - r1) / r2
    PH = np.broadcast_to(phis[:, None], (nu, nv))
    P = S.point(PH, Z)
    # the back panel (hidden against the hero) is its own UV island with less texel density: seams at the two back corners instead of at the back centre
    k = int(round(0.115 / (S.L / nu)))
    d_seam = np.ones((nu, nv)); d_seam[k, :] = 0.0; d_seam[nu - k, :] = 0.0
    fields = dict(phi=wrap_pi(PH), zc=Z, d_b=Z - zb[:, None], d_seam=d_seam)
    fab = np.zeros((nu, nv - 1), np.int32); fab[:, :r1] = FAB['leather']; fab[:, r1:] = FAB['twill']
    uvw = np.full((nu, nv - 1), UVW['shell_front'], np.int32); uvw[:k, :] = UVW['shell_back']; uvw[nu - k:, :] = UVW['shell_back']
    ob = BG.sheet_grid('Shell', P, fields, wrap_u=True, face_attr=dict(fabric=fab, uvw100=uvw))
    G.set_face_attr(ob, 'kind', K['SHELL']); G.set_face_attr(ob, 'sub', 0)
    return ob, phis


def build_lining(S, nu=64, depth=0.075, nv=5, thick=0.0035):
    phis = S.grid_phi(nu)
    zs = np.linspace(S.ZT - 0.001, S.ZT - depth, nv)
    PH = np.broadcast_to(phis[:, None], (nu, nv)); Z = np.broadcast_to(zs[None, :], (nu, nv))
    P = S.point(PH, Z) - S.normal(PH, Z) * thick
    d_seam = np.ones((nu, nv)); d_seam[0, :] = 0.0
    fields = dict(phi=wrap_pi(PH), zc=Z, d_seam=d_seam, depth=S.ZT - Z)
    ob = BG.sheet_grid('Lining', P, fields, wrap_u=True)
    set_kind(ob, K['LINING'], FAB['twill'])
    # floor
    zf = zs[-1]
    ring = P[:, -1]
    c = np.array([0.0, S.yc(zf) if hasattr(S, 'yc') else 0.0, zf])
    m = BG.SheetMesh()
    b0 = m.add_points(ring, phi=wrap_pi(phis), zc=np.full(nu, zf), d_seam=np.ones(nu), depth=np.full(nu, S.ZT - zf))
    cb = m.add_points(c[None], phi=0.0, zc=zf, d_seam=1.0, depth=S.ZT - zf)
    for i in range(nu):
        m.add_face([cb, b0 + i, b0 + (i + 1) % nu], 0)
    fl = m.to_object('Floor')
    set_kind(fl, K['FLOOR'], FAB['twill'])
    return ob, fl


def build_base(S, nu=64, cap=True):
    prof = [(0.000, -0.015), (0.0025, -0.0090), (0.007, -0.0030), (0.013, 0.0018), (0.024, 0.0042), (0.037, 0.0046), (0.045, 0.0036), (0.0508, 0.0008)]
    phis = S.grid_phi(nu)
    nv = len(prof)
    PH = np.broadcast_to(phis[:, None], (nu, nv))
    zr = np.array([p[0] for p in prof]); of = np.array([p[1] for p in prof])
    Nh = S.hnormal(phis, S.Z0)
    base = S.point(phis, S.Z0)
    P = np.zeros((nu, nv, 3))
    for j in range(nv):
        P[:, j, :2] = base[:, :2] + Nh[:, :2] * of[j]; P[:, j, 2] = zr[j]
    d_seam = np.ones((nu, nv)); d_seam[0, :] = 0.0
    fields = dict(phi=wrap_pi(PH), zc=np.broadcast_to(zr[None, :], (nu, nv)), d_seam=d_seam)
    ob = BG.sheet_grid('Base', P, fields, wrap_u=True)
    # bottom cap
    m = BG.SheetMesh()
    ring = P[:, 0]
    b0 = m.add_points(ring, phi=wrap_pi(phis), zc=np.zeros(nu), d_seam=np.ones(nu))
    cb = m.add_points(np.array([[0.0, float(np.mean(ring[:, 1])), 0.0]]), phi=0.0, zc=0.0, d_seam=1.0)
    for i in range(nu):
        m.add_face([cb, b0 + (i + 1) % nu, b0 + i], 0)
    capob = m.to_object('BaseCap')
    set_kind(ob, K['BASE'], FAB['plastic']); set_kind(capob, K['BASE'], FAB['plastic'])
    if not cap:
        bpy.data.objects.remove(capob)
        return [ob]
    return [ob, capob]


Shell.yc = lambda self, z: self.YB - self.prof(z, self.B)


def surf_tube(S, name, s_pts, z_pts, radius, off, sides=6, closed=False, fields=None, caps=True):
    path = S.at(np.asarray(s_pts), np.asarray(z_pts), off)
    L = BG.arclen(path)
    fl = dict(zl=L, phi=wrap_pi(S.last_phi), zc=np.asarray(z_pts, float), d_seam=np.ones(len(path)))
    if fields: fl.update(fields)
    return G.tube(name, path, radius, sides=sides, closed=closed, caps=caps, fields=fl)


def build_rim(S, nu=64):
    phis = S.grid_phi(nu)
    P = S.point(phis, S.ZT) - S.hnormal(phis, S.ZT) * 0.0010
    P[:, 2] = S.ZT + 0.0030
    fl = dict(zl=np.linspace(0, S.L, nu), phi=wrap_pi(phis), zc=np.full(nu, S.ZT), d_seam=np.ones(nu))
    rim = G.tube('Rim', P, 0.0148, sides=12, closed=True, fields=fl)
    set_kind(rim, K['RIM'], FAB['leather'])
    return rim


def build_pipings(S, nu=96):
    out = []
    s_all = np.linspace(-S.L / 2, S.L / 2, nu, endpoint=False)
    # under the rim
    p = surf_tube(S, 'PipeRim', s_all, np.full(nu, S.ZT - 0.0185), 0.0023, 0.0011, sides=6, closed=True)
    set_kind(p, K['PIPING'], FAB['twill']); out.append(p)
    # boot seam: follows zb(phi): needs phi per s at its own height -> iterate: solve z=zb(phi(s,z)) by two passes
    z = np.full(nu, 0.20)
    for _ in range(4):
        S.at(s_all, z, 0.0); z = S.zb(S.last_phi)
    p = surf_tube(S, 'PipeBoot', s_all, z, 0.0021, 0.0010, sides=6, closed=True)
    set_kind(p, K['PIPING'], FAB['twill']); out.append(p)
    return out


def build_dividers(S, top_drop=0.020, floor_depth=0.075):
    H = floor_depth - top_drop
    zc = S.ZT - top_drop - H / 2
    b_in = S.prof(S.ZT, S.B) - 0.0035 - 0.0015; a_in = S.prof(S.ZT, S.A) - 0.0035 - 0.0015
    yc = float(S.yc(S.ZT))
    bx = BG.rbox('DivX', (2 * a_in, 0.0046, H), bevel=0.0013, seg=2, center=(0, yc, zc))
    by = BG.rbox('DivY', (0.0046, 2 * b_in, H - 0.0006), bevel=0.0013, seg=2, center=(0, yc, zc - 0.0003))
    for o in (bx, by): set_kind(o, K['DIVIDER'], FAB['twill'])
    return [bx, by]


# ====================================================================== bands (straps, handle)
def band(name, C, Nrm, hw, T0, crown, nv=7, edge_steps=3, extra=None, pad_fn=None):
    """a flat padded band along the polyline C (centre of the BODY-SIDE surface), Nrm = unit direction facing away from the body; hw half width (ns,), T0 thickness at the edges,
    crown = extra bulge at the centre. Returns the thick shell object (zones 0 outer / 1 rim / 2 inner)."""
    ns = len(C)
    tang = unit(np.gradient(C, axis=0))
    W = unit(np.cross(Nrm, tang)); Nn = np.cross(tang, W)
    x = np.linspace(-1, 1, nv)
    cr = crown[:, None] * (1 - x[None, :] ** 2) ** 0.6
    Tt = T0[:, None] + cr
    P = C[:, None, :] + W[:, None, :] * (hw[:, None] * x[None, :])[..., None] + Nn[:, None, :] * Tt[..., None]
    sa = BG.arclen(C)
    fields = dict(sa=np.broadcast_to(sa[:, None], (ns, nv)), sx=hw[:, None] * x[None, :], hwid=np.broadcast_to(hw[:, None], (ns, nv)),
                  tot=np.full((ns, nv), sa[-1]), d_seam=np.ones((ns, nv)))
    if extra: fields.update({k: np.broadcast_to(np.asarray(v)[:, None] if np.ndim(v) == 1 else v, (ns, nv)) for k, v in extra.items()})
    ob = G.thick_grid(name, P, Tt, edge_steps=edge_steps, rim_u=(True, True), rim_v=(True, True), fields=fields)
    return ob, (tang, W, Nn)


def strap_profile(sa, tot, hw_pad=0.0255, hw_web=0.0135, top_tab=0.040, taper=0.040, low_web=0.215):
    """width / thickness / crown of the strap along its length (metres): top webbing tab, taper, padded section, taper, lower webbing."""
    s1 = top_tab; s2 = top_tab + taper; s3 = tot - low_web - taper; s4 = tot - low_web
    pad = sstep(s1, s2, sa) * (1 - sstep(s3, s4, sa))
    hw = hw_web + (hw_pad - hw_web) * pad
    T0 = 0.0030 + 0.0010 * pad
    crown = 0.0060 * pad
    return hw, T0, crown, pad


def mid_normals(hints, ns):
    """linearly interpolate hint normals (k,3) over ns samples and renormalise."""
    k = len(hints); t = np.linspace(0, k - 1, ns)
    i0 = np.clip(np.floor(t).astype(int), 0, k - 2); f = (t - i0)[:, None]
    return unit(hints[i0] * (1 - f) + hints[i0 + 1] * f)


def dense_with_hints(ctrl, hints, n_per=14):
    C = BG.catmull(np.asarray(ctrl), n_per)
    k = len(ctrl)
    idx = np.arange(len(C)) / n_per
    Hh = np.stack([np.interp(idx, np.arange(k), np.asarray(hints)[:, j]) for j in range(3)], 1)
    return C, unit(Hh)


def build_strap(name, ctrl, hints, ns=110, low_web=0.34):
    """ctrl: control points (centre of the body-side surface); hints: unit normals (facing away from the body) at the control points."""
    Cd, Hd = dense_with_hints(ctrl, hints)
    sd = BG.arclen(Cd)
    sa = np.linspace(0, sd[-1], ns)
    C = np.stack([np.interp(sa, sd, Cd[:, k]) for k in range(3)], 1)
    Nh = unit(np.stack([np.interp(sa, sd, Hd[:, k]) for k in range(3)], 1))
    tot = sa[-1]
    hw, T0, crown, pad = strap_profile(sa, tot, low_web=low_web)
    ob, frame = band(name, C, Nh, hw, T0, crown, nv=7, extra=dict(sec=pad))
    sec_f = face_mean(ob, 'sec')
    fab = np.where(sec_f > 0.5, FAB['leather'], FAB['rib']).astype(np.int32)
    G.set_face_attr(ob, 'kind', K['STRAP']); G.set_face_attr(ob, 'sub', 0)
    set_faces_from(ob, 'fabric', fab)
    return ob, dict(C=C, sa=sa, tot=tot, frame=frame, N=Nh)


def build_buckle(name, strap_info, at_from_end=0.115):
    C, sa, tot = strap_info['C'], strap_info['sa'], strap_info['tot']
    tang, W, Nn = strap_info['frame']
    s0 = tot - at_from_end
    c = np.array([np.interp(s0, sa, C[:, k]) for k in range(3)])
    t_ = unit(np.array([np.interp(s0, sa, tang[:, k]) for k in range(3)]))
    w_ = unit(np.array([np.interp(s0, sa, W[:, k]) for k in range(3)]))
    n_ = unit(np.cross(t_, w_))
    c = c + n_ * 0.0016
    ww, hh = 0.0215, 0.0118
    th = np.linspace(0, 2 * math.pi, 40, endpoint=False)
    cx, sx = np.cos(th), np.sin(th)
    xs = ww * np.sign(cx) * np.abs(cx) ** 0.28; ys = hh * np.sign(sx) * np.abs(sx) ** 0.28
    path = np.array([c + w_ * a + t_ * b for a, b in zip(xs, ys)])
    fl = dict(zl=np.zeros(len(path)), d_seam=np.ones(len(path)))
    frame = G.tube(name + '_Frame', path, 0.0021, sides=8, closed=True, fields=fl)
    bar = np.array([c + w_ * a for a in np.linspace(-ww, ww, 8)])
    barob = G.tube(name + '_Bar', bar, 0.0017, sides=6, closed=False, fields=dict(zl=np.zeros(len(bar)), d_seam=np.ones(len(bar))))
    for o in (frame, barob): set_kind(o, K['BUCKLE'], FAB['plastic'])
    return [frame, barob]




# ====================================================================== patches (pockets, hub), zips, pulls
def rrect_grid(nu, nv, hw, hh, p=5.0):
    u = np.linspace(-1, 1, nu); v = np.linspace(-1, 1, nv)
    U, V = np.meshgrid(u, v, indexing='ij')
    g = (np.abs(U) ** p + np.abs(V) ** p) ** (1 / p)
    m = np.maximum(np.abs(U), np.abs(V))
    f = np.where(g > 1e-9, m / np.maximum(g, 1e-9), 1.0)
    X = U * f * hw; Y = V * f * hh
    Gv = (np.abs(X / hw) ** p + np.abs(Y / hh) ** p) ** (1 / p)
    gx = np.where(Gv > 1e-9, Gv ** (1 - p) * np.abs(X / hw) ** (p - 1) / hw, 1 / hw)
    gy = np.where(Gv > 1e-9, Gv ** (1 - p) * np.abs(Y / hh) ** (p - 1) / hh, 1 / hh)
    d_edge = np.clip((1 - Gv) / np.maximum(np.hypot(gx, gy), 1e-9), 0, None)
    return X, Y, d_edge


def build_patch(S, name, s0, z0, hw, hh, off, T, kind, fabric, nu=17, nv=21, p=5.0):
    X, Y, de = rrect_grid(nu, nv, hw, hh, p)
    P = S.at(s0 + X, z0 + Y, off)
    fields = dict(px=X, py=Y, d_edge=de, d_seam=np.ones_like(X))
    ob = G.thick_grid(name, P, T, edge_steps=3, rim_u=(True, True), rim_v=(True, True), fields=fields)
    set_kind(ob, kind, fabric)
    return ob


def surf_path(S, pts_sz, n=60):
    """dense (s, z) polyline through control points (k,2) -> (n,) s, z"""
    c = BG.catmull(np.c_[np.asarray(pts_sz), np.zeros(len(pts_sz))], 10)[:, :2]
    pts, _ = BG.resample(np.c_[c, np.zeros(len(c))], n)
    return pts[:, 0], pts[:, 1]


def build_zip(S, name, pts_sz, width=0.0085, n=48, off=0.0007):
    s, z = surf_path(S, pts_sz, n)
    C = S.at(s, z, off)
    N = S.last_normal.copy()
    hw = np.full(n, width / 2); T0 = np.full(n, 0.0016); cr = np.full(n, 0.0004)
    ob, frame = band(name, C, N, hw, T0, cr, nv=5, edge_steps=2)
    set_kind(ob, K['ZIP'], FAB['rib'])
    return ob, (C, N, frame)


def build_pull(name, C, N, frame, at_end=True, length=0.021):
    tang, W, Nn = frame
    i = len(C) - 1 if at_end else 0
    t_ = tang[i] * (1 if at_end else -1)
    c = C[i] + Nn[i] * 0.0035 + t_ * (0.004 if at_end else 0.004)
    R = np.stack([W[i], Nn[i], t_], 1)
    ob = BG.rbox(name, (0.0075, 0.0034, length), bevel=0.0013, seg=2, center=c, R=R)
    set_kind(ob, K['PULL'], FAB['plastic'])
    slider = BG.rbox(name + '_S', (0.0115, 0.0045, 0.0085), bevel=0.0014, seg=2, center=C[i] + Nn[i] * 0.0022 - t_ * 0.001, R=R)
    set_kind(slider, K['PULL'], FAB['plastic'])
    return [ob, slider]


def build_studs(S):
    out = []
    for sg in (-1, 1):
        s = sg * 0.074
        P = S.at(np.array([s]), np.array([S.ZT - 0.0370]), 0.0)[0]; N = S.last_normal[0]
        prof = [(0.0049, 0.0), (0.0052, 0.0006), (0.0046, 0.0016), (0.0032, 0.0026), (0.0, 0.0031)]
        ob = G.lathe('Stud', prof, sides=12)
        R = BG.rot_to(np.array([0, 0, 1.0]), N)
        xform(ob, R, P)
        set_kind(ob, K['STUD'], FAB['plastic']); out.append(ob)
    return out


# ====================================================================== handle (padded webbing arch at the back of the rim)
def build_handle(S):
    n = 44
    t = np.linspace(0, math.pi, n)
    y0 = S.YB - 0.012
    ztop = S.ZT + 0.003 + 0.0148
    x = 0.062 * np.cos(t); z = ztop - 0.002 + 0.048 * np.sin(t) ** 0.8
    C = np.stack([x, np.full(n, y0), z], 1)
    ctr = np.array([0.0, y0, ztop - 0.012])
    Nh = unit(C - ctr)
    hw = np.full(n, 0.0155); T0 = np.full(n, 0.0040); cr = np.full(n, 0.0030)
    ob, fr = band('Handle', C, Nh, hw, T0, cr, nv=5, edge_steps=3)
    set_kind(ob, K['HANDLE'], FAB['leather'])
    return ob


# ====================================================================== stand legs
LEG = dict(r_up=0.0066, r_lo=0.0056, hinge_z=0.672, x_corner=0.104, up_len=0.36, fold_z=0.080, open_foot=(0.215, 0.215), gap=0.0030)


def leg_s(S, sg):
    """arc coordinate of the back corner line the legs fold into (x ~ +-x_corner at hinge height)."""
    phis, ss = S.table(LEG['hinge_z'])
    P = S.point(phis, LEG['hinge_z'])
    ok = (P[:, 1] > 0.02) & (np.sign(P[:, 0]) == sg)
    idx = np.nonzero(ok)[0]
    j = idx[np.argmin(np.abs(np.abs(P[idx, 0]) - LEG['x_corner']))]
    return float(ss[j])


def leg_hinge(S, sg):
    return S.at(np.array([leg_s(S, sg)]), np.array([LEG['hinge_z']]), LEG['r_up'] + LEG['gap'])[0]


def build_bracket(S):
    L = LEG
    out = []
    n = 30
    s = S.L / 2 + np.linspace(-0.118, 0.118, n)
    C = S.at(s, np.full(n, 0.690), 0.0004)
    N = S.last_normal.copy()
    ob, fr = band('Bracket', C, N, np.full(n, 0.0185), np.full(n, 0.0036), np.full(n, 0.0007), nv=5, edge_steps=3)
    set_kind(ob, K['BRACKET'], FAB['plastic']); out.append(ob)
    for sg in (-1, 1):
        H = leg_hinge(S, sg)
        hing = G.lathe('Hinge', [(0.0090, -0.0125), (0.0090, 0.0125)], sides=14)
        tang = unit(np.cross([0, 0, 1.0], S.last_normal[0])) if False else np.array([1.0, 0, 0])
        xform(hing, BG.rot_to(np.array([0, 0, 1.0]), tang), H)
        set_kind(hing, K['HINGE'], FAB['plastic']); out.append(hing)
    return out


def build_legs(S, pose='folded'):
    L = LEG
    out = []
    for sg in (-1, 1):
        H = leg_hinge(S, sg)
        if pose == 'folded':
            Fp = S.at(np.array([leg_s(S, sg)]), np.array([L['fold_z']]), L['r_up'] + L['gap'])[0]
            d = unit(Fp - H); length = float(np.linalg.norm(Fp - H)) + 0.012
        else:
            F = np.array([sg * L['open_foot'][0], S.YB + L['open_foot'][1], 0.0155])
            d = unit(F - H); length = float(np.linalg.norm(F - H))
        up = G.tube('LegUp', np.array([H, H + d * L['up_len']]), L['r_up'], sides=10, caps=True, fields=dict(zl=np.array([0, L['up_len']])))
        set_kind(up, K['LEG'], FAB['plastic']); out.append(up)
        p0 = H + d * (L['up_len'] - 0.004); p1 = H + d * length
        lo = G.tube('LegLo', np.array([p0, p1]), L['r_lo'], sides=10, caps=True, fields=dict(zl=np.array([0, 1.0])))
        set_kind(lo, K['LEG'], FAB['plastic']); out.append(lo)
        col = G.tube('LegCollar', np.array([H + d * (L['up_len'] - 0.012), H + d * (L['up_len'] + 0.006)]), L['r_up'] + 0.0012, sides=10, caps=True, fields=dict(zl=np.array([0, 1.0])))
        set_kind(col, K['LEG'], FAB['plastic']); out.append(col)
        prof = [(0.0072, 0.0), (0.0086, 0.004), (0.0088, 0.014), (0.0072, 0.0215), (0.0036, 0.0242), (0.0, 0.0250)]
        ft = G.lathe('LegFoot', prof, sides=12)
        xform(ft, BG.rot_to(np.array([0, 0, 1.0]), d), p1 - d * 0.004)
        set_kind(ft, K['FOOT'], FAB['plastic']); out.append(ft)
    return out


# ====================================================================== clubs, head covers
def lathe_local(name, profile, sides=16, scale_xy=(1, 1)):
    return G.lathe(name, profile, sides=sides, scale_xy=scale_xy)


COVER_CUFF_Z = 0.034
COVER_SOCK = [(0.0145, 0.0), (0.0160, 0.004), (0.0185, 0.012), (0.0215, 0.022), (0.0252, 0.030)]
COVER_BODY = [(0.0325, 0.040), (0.0398, 0.054), (0.0442, 0.070), (0.0450, 0.086), (0.0416, 0.099), (0.0368, 0.108), (0.0306, 0.1155), (0.0235, 0.1215), (0.0150, 0.1262), (0.0070, 0.1290), (0.0, 0.1300)]
COVER_TOP_Z = 0.112


def build_cover(name, axis, base_pt, t0, pattern, scale=1.0, spin=0.0):
    prof = [(r * scale, z * scale) for r, z in COVER_SOCK + COVER_BODY]
    ob = G.lathe(name, prof, sides=24, scale_xy=(1.0, 1.10))
    P = G.positions(ob)
    # fields in the cover's own frame (before it is rotated / placed)
    kphi = np.arctan2(P[:, 1] / 1.10, P[:, 0]); kz = P[:, 2] / scale
    BG.set_point_float(ob, 'kphi', kphi); BG.set_point_float(ob, 'kz', kz); BG.set_point_float(ob, 'kpat', np.full(len(P), float(pattern)))
    BG.set_point_float(ob, 'd_seam', np.ones(len(P)))
    me = ob.data
    zf = np.array([P[list(p.vertices), 2].mean() for p in me.polygons]) / scale
    G.set_face_attr(ob, 'kind', K['COVER']); G.set_face_attr(ob, 'sub', 0)
    set_faces_from(ob, 'fabric', np.where(zf < COVER_CUFF_Z, FAB['rib'], FAB['pique']))
    R = BG.rot_to(np.array([0, 0, 1.0]), axis) @ np.array([[math.cos(spin), -math.sin(spin), 0], [math.sin(spin), math.cos(spin), 0], [0, 0, 1]])
    xform(ob, R, base_pt + axis * t0)
    return ob


GRIP_PROFILE = [(0.0093, 0.0), (0.0104, 0.04), (0.0119, 0.150), (0.0122, 0.1585), (0.0110, 0.1632), (0.0080, 0.1655), (0.0, 0.1660)]


def build_grip(name, axis, base_pt, t_top, length=0.166, white_band=True):
    prof = [(r, z * length / 0.166 - length) for r, z in GRIP_PROFILE]
    ob = G.lathe(name, prof, sides=12)
    P = G.positions(ob)
    BG.set_point_float(ob, 'kz', P[:, 2]); BG.set_point_float(ob, 'kphi', np.arctan2(P[:, 1], P[:, 0])); BG.set_point_float(ob, 'd_seam', np.ones(len(P)))
    set_kind(ob, K['GRIP'], FAB['rib'])
    R = BG.rot_to(np.array([0, 0, 1.0]), axis)
    xform(ob, R, base_pt + axis * t_top)
    return ob


def build_shaft(name, axis, base_pt, t0, t1, r=0.0042):
    pts = np.array([base_pt + axis * t0, base_pt + axis * t1])
    ob = G.tube(name, pts, r, sides=7, caps=True, fields=dict(zl=np.array([0.0, 1.0]), d_seam=np.ones(2)))
    set_kind(ob, K['SHAFT'], FAB['plastic'])
    return ob


def build_iron(name, axis, base_pt, t_head, spin=0.0, L=0.076, H=0.041, T=0.0105):
    d = unit(axis)
    e1, e2, _ = BG.frame_from_axis(d, (math.cos(spin), math.sin(spin), 0))
    e2 = np.cross(d, e1)
    t_ = unit(e1 * math.sin(math.radians(62)) + d * math.cos(math.radians(62)))
    h_ = unit(e1 * math.cos(math.radians(62)) - d * math.sin(math.radians(62)))
    heel = base_pt + d * t_head
    nu, nv = 14, 6
    u = np.linspace(0, 1, nu); v = np.linspace(0, 1, nv)
    prof = 0.42 + 0.58 * np.sin(np.pi * np.clip(u * 0.82 + 0.06, 0, 1)) ** 0.8
    pr = np.where(u > .9, prof * (1 - ((u - .9) / .1) ** 2 * .55), prof)
    P = np.zeros((nu, nv, 3))
    for i in range(nu):
        for j in range(nv):
            P[i, j] = heel + t_ * (L * u[i]) + h_ * (H * v[j] * pr[i]) + e2 * (-0.0 * v[j])
    # grid orientation: du x dv = t x h = e2 (checked in the docstring of the builder); the outer face looks along +e2
    ob = G.thick_grid(name, P, T, edge_steps=3, rim_u=(True, True), rim_v=(True, True), fields=dict(d_seam=np.ones((nu, nv)), zl=np.zeros((nu, nv))))
    set_kind(ob, K['IRON'], FAB['plastic'])
    return ob


# the layout (rim-plane position, tilt degrees from vertical, azimuth of the lean in degrees from +X, ...)
CLUBS = [
    dict(kind='wood', name='Driver', xy=(-0.060, -0.040), tilt=12, az=212, t_c=0.028, pat=0, scale=1.12, spin=0.3),
    dict(kind='wood', name='Fairway', xy=(0.012, -0.048), tilt=4, az=270, t_c=0.040, pat=1, scale=1.06, spin=-0.1),
    dict(kind='wood', name='Hybrid', xy=(0.072, -0.040), tilt=13, az=338, t_c=0.020, pat=2, scale=1.00, spin=-0.5),
    dict(kind='iron', name='Iron1', xy=(0.042, 0.042), tilt=6, az=90, t_head=0.064, spin=0.6),
    dict(kind='iron', name='Iron2', xy=(0.082, 0.048), tilt=9, az=25, t_head=0.042, spin=-0.4),
    dict(kind='grip', name='Putter', xy=(-0.092, -0.062), tilt=5, az=200, t_top=0.052),
    dict(kind='grip', name='Wedge1', xy=(-0.040, 0.046), tilt=4, az=100, t_top=0.046),
    dict(kind='grip', name='Wedge2', xy=(-0.082, 0.042), tilt=7, az=150, t_top=0.038),
]


def build_clubs(S):
    out = []
    for c in CLUBS:
        tl, az = math.radians(c['tilt']), math.radians(c['az'])
        d = np.array([math.sin(tl) * math.cos(az), math.sin(tl) * math.sin(az), math.cos(tl)])
        base = np.array([c['xy'][0], c['xy'][1], S.ZT])
        low = -0.095
        if c['kind'] == 'wood':
            out.append(build_shaft('Shaft_' + c['name'], d, base, low, c['t_c'] + 0.040))
            out.append(build_cover('Cover_' + c['name'], d, base, c['t_c'], c['pat'], c['scale'], c['spin']))
        elif c['kind'] == 'iron':
            out.append(build_shaft('Shaft_' + c['name'], d, base, low, c['t_head'] + 0.002))
            out.append(build_iron('Head_' + c['name'], d, base, c['t_head'], c['spin']))
        else:
            out.append(build_grip('Grip_' + c['name'], d, base, c['t_top']))
    return out


# ====================================================================== product straps
def product_strap_ctrl(S, side=+1):
    """control points + hints of one product strap, from the hub tab over the arch, down the side wall, past the buckle to the lower anchor on the side wall.
    side=+1: the strap on the +X side (viewed from the front), -1 its mirror."""
    L2 = S.L / 2
    spec = [(0.040, 0.600, 0.0050), (0.052, 0.640, 0.0050), (0.066, 0.690, 0.0050), (0.080, 0.728, 0.0060), (0.094, 0.758, 0.0185), (0.115, 0.800, 0.0255),
            (0.150, 0.830, 0.0335), (0.185, 0.805, 0.0300), (0.205, 0.760, 0.0185), (0.212, 0.715, 0.0045), (0.214, 0.640, 0.0006), (0.215, 0.550, 0.0006),
            (0.216, 0.450, 0.0006), (0.214, 0.380, 0.0006), (0.212, 0.320, 0.0006), (0.210, 0.265, 0.0006)]
    pts = []; hn = []
    for dx, z, o in spec:
        sv = side * (L2 - dx)
        p = S.at(np.array([sv]), np.array([z]), o)[0]; n = S.last_normal[0].copy()
        arch = sstep(0.74, 0.84, z) * (1 if o > 0.012 else 0)
        pts.append(p); hn.append(unit(n + np.array([0, 0, 0.9 * arch])))
    return np.array(pts), np.array(hn)


TAIL_IDX = 11


def build_hub(S):
    return build_patch(S, 'Hub', S.L / 2, 0.600, 0.031, 0.031, 0.0148, 0.0032, K['HUB'], FAB['leather'], nu=13, nv=13, p=4.0)


def build_product(pose='folded', straps=(1, -1), clubs=True, S=None, strap_override=None, drop_hidden=False, low_web=0.34):
    A.reset()
    S = S or Shell()
    parts = []
    sh, phis = build_shell(S); parts.append(sh)
    li, fl = build_lining(S); parts += [li, fl]
    parts += build_base(S, cap=not drop_hidden)
    parts.append(build_rim(S))
    parts += build_pipings(S)
    parts += build_dividers(S)
    parts.append(build_handle(S))
    parts += build_studs(S)
    # ---- front: big pocket with a welted zip across its top, vertical zip pocket, lower outlined pocket with its zip
    parts.append(build_patch(S, 'FrontPocket', -0.056, 0.505, 0.046, 0.092, 0.0034, 0.0032, K['POCKET'], FAB['leather'], nu=19, nv=25, p=5.0))
    z1, fz1 = build_zip(S, 'ZipPocketTop', [(-0.100, 0.582), (-0.056, 0.586), (-0.012, 0.582)], n=22)
    parts.append(z1); parts += build_pull('PullA', *fz1, at_end=True)
    z2, fz2 = build_zip(S, 'ZipVertical', [(0.028, 0.320), (0.029, 0.440), (0.029, 0.580), (0.027, 0.700)], n=36)
    parts.append(z2); parts += build_pull('PullB', *fz2, at_end=True)
    parts.append(build_patch(S, 'LowerPocket', -0.040, 0.262, 0.060, 0.030, 0.0016, 0.0020, K['POCKET2'], FAB['twill'], nu=19, nv=11, p=3.0))
    z3, fz3 = build_zip(S, 'ZipLower', [(-0.092, 0.288), (-0.040, 0.292), (0.012, 0.288)], n=22)
    parts.append(z3); parts += build_pull('PullC', *fz3, at_end=False)
    # ---- side: long outlined zip pocket
    parts.append(build_patch(S, 'SidePocket', 0.131, 0.540, 0.026, 0.186, 0.0016, 0.0020, K['POCKET2'], FAB['twill'], nu=9, nv=31, p=3.0))
    z4, fz4 = build_zip(S, 'ZipSide', [(0.131, 0.375), (0.132, 0.480), (0.132, 0.600), (0.131, 0.706)], n=34)
    parts.append(z4); parts += build_pull('PullD', *fz4, at_end=True)
    # ---- back (hidden once worn: dropped from the exported mesh)
    if not drop_hidden:
        parts.append(build_patch(S, 'BackPocket', S.L / 2, 0.222, 0.082, 0.113, 0.0034, 0.0032, K['BACKPOCKET'], FAB['leather'], nu=17, nv=21, p=3.5))
        parts.append(build_hub(S))
    parts += build_bracket(S)
    parts += build_legs(S, pose)
    if clubs: parts += build_clubs(S)
    infos = {}
    for sg in straps:
        ctrl, hints = product_strap_ctrl(S, sg) if strap_override is None else strap_override[sg]
        ob, info = build_strap('Strap_%s' % ('P' if sg > 0 else 'N'), ctrl, hints, low_web=low_web)
        parts.append(ob); parts += build_buckle(ob.name + '_Buckle', info)
        infos[sg] = info
    return parts, S, infos


def show(parts, outdir, prefix, views=None, res=(800, 900)):
    import bag_view as V
    V.flat_materials(parts)
    views = views or {'front': (0, 8), 'side': (90, 8), 'back': (180, 8), '34': (38, 20), 'back34': (150, 20), 'top': (0, 78)}
    V.views_render(outdir, prefix, views, res=res)


if __name__ == '__main__':
    args = A.args_after_dashes()
    out = args[0]; pose = args[1] if len(args) > 1 else 'folded'
    parts, S, infos = build_product(pose)
    tot = 0
    for p in parts:
        st = G.mesh_stats(p); tot += st['tris']
    print('TOTAL tris', tot, 'objects', len(parts))
    bpy.ops.wm.save_as_mainfile(filepath=out)
    if len(args) > 2:
        show(parts, args[2], 'bag_' + pose)
