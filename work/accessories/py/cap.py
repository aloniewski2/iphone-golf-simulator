"""ACCESSORIES: the cap, built alone as a product (tennis performance cap / golf tour cap).
Blender -b --factory-startup --python cap.py -- <tennis|golf> <out.blend>

Product frame = N frame (Z up, front -Y, hero's left +X); origin = centre of the band plane (the plane of the crown's lower edge), x=y=0 at the
ellipse centre. Real construction: six-panel crown with a shallow crease at every seam and soft puffed panels, wall thickness 3.5 mm with a rounded
lower edge, a lining surface that is its own UV island, a thick curved brim (stiffener + two faces + rounded edge), a sweatband that rolls over the
lower edge inside, a covered button, piped seams (tennis), a rear arch with an adjustable strap and slider buckle (tennis) / punched snap strap (golf).
Per-point fields feed the map baker (seams, stitching, vents, patch): phi, zc, d_seam, d_edge, d_root, px, py, sx, sy.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import acc_common as A
import acc_geo as G

# kinds (face attribute `kind`) read by the map baker
K_CROWN, K_BRIM, K_BAND, K_STRAP, K_BUTTON, K_PIPING, K_PATCH, K_BUCKLE = range(8)
FAB = {'twill': 6, 'pique': 7, 'leather': 8, 'rib': 9, 'plastic': 4}

BASE = dict(
    A=0.0990, B=0.1140,          # outer semi-axes of the crown at the band plane (x = across, y = front-back)
    H=0.1080,                    # height of the outer apex above the band plane (the fit cuts the crown to each head: z scale)
    T=0.0035,                    # wall thickness of the crown
    e=0.72, ez=0.74,             # dome: rad = c*cos(u)^e (straight walls first, then the round top), z = H*sin(u)^ez
    panels=6, seg=8, rows=22,
    puff=0.0011, groove=0.0008,
    notch_half=math.radians(27), notch_h=0.026,
    brim=dict(L0=0.0680, psi=math.radians(64), p0=12.0, p1=37.0, T=0.0044, z0=0.0022, cols=37, rows=9, fwd=0.60, curl=2.0),
    band=dict(h=0.0112, T=0.0028, drop=0.0010),
    strap=dict(w=0.0185, T=0.0027, z=0.0090),
    button=dict(r=0.0074, h=0.0046),
)
VARIANTS = {
    'tennis': dict(piping=True, vents=True, patch=False, strap_kind='webbing', brim_rows=3, colours=dict(base='F3F2EE', trim='1F2B57')),
    'golf': dict(piping=False, vents=False, patch=True, strap_kind='snap', brim_rows=4, colours=dict(base='1F2B57', trim='F3F2EE')),
}


def wrap_pi(a): return (np.asarray(a) + math.pi) % (2 * math.pi) - math.pi


class Crown:
    def __init__(self, D):
        self.D = D

    def uz(self, z):
        """elevation parameter u (0 at the band plane) for height z."""
        return np.arcsin(np.clip(z / self.D['H'], 0, 1) ** (1 / self.D['ez']))

    def point(self, phi, u):
        D = self.D
        c = np.cos(u) ** D['e']; z = D['H'] * np.sin(u) ** D['ez']
        return np.stack([D['A'] * c * np.sin(phi), -D['B'] * c * np.cos(phi), z], -1)

    def radial_at(self, phi, z):
        """crown outer point at azimuth phi and height z (ignoring puff / creases)."""
        return self.point(phi, self.uz(z))

    def notch_z(self, phi):
        D = self.D
        d = np.abs(wrap_pi(phi - math.pi)) / D['notch_half']
        return D['notch_h'] * np.sqrt(np.clip(1 - d * d, 0, 1))


def build_crown(D, V):
    C = Crown(D)
    nu = D['panels'] * D['seg']; nv = D['rows']
    phis = np.arange(nu) * 2 * math.pi / nu
    z0 = C.notch_z(phis)
    w0 = C.uz(z0) / (math.pi / 2)
    s = (np.arange(nv) / (nv - 1)) ** 1.12
    w = w0[:, None] + (1 - w0[:, None]) * s[None, :]
    u = w * math.pi / 2
    PH = np.broadcast_to(phis[:, None], (nu, nv))
    P0 = C.point(PH, u)
    n0 = G.grid_normals(P0, wrap_u=True, pole_top=True)
    # panel puff + crease at every seam
    frac = (phis / (2 * math.pi / D['panels'])) % 1.0
    frac = np.broadcast_to(frac[:, None], (nu, nv))
    fade = (1 - np.clip(w, 0, 1) ** 5)
    puff = D['puff'] * np.sin(math.pi * frac) ** .75 * fade * (0.55 + .45 * np.sin(math.pi * np.clip(w * 1.1, 0, 1)))
    seam_col = (np.arange(nu) % D['seg'] == 0)[:, None] & np.ones((1, nv), bool)
    groove = D['groove'] * seam_col
    P = P0 + n0 * (puff - groove)[..., None]
    # fields
    zc = P[..., 2]
    ang = np.abs(wrap_pi(PH[..., None] - np.arange(D['panels'])[None, None, :] * 2 * math.pi / D['panels']))
    rr = 0.5 * (D['A'] + D['B']) * np.cos(u) ** D['e']
    d_seam = ang.min(-1) * rr
    phis_s = wrap_pi(PH)
    fields = dict(phi=phis_s, zc=zc, d_seam=d_seam, w=w)
    ob = G.thick_grid('Crown', P, D['T'], wrap_u=True, pole_top=True, edge_steps=4, rim_v=(True, False), fields=fields)
    return ob, C


def brim_geometry(D, V, C):
    B = D['brim']
    nu, nv = B['cols'], B['rows']
    psis = np.linspace(-B['psi'], B['psi'], nu)
    v = np.linspace(0, 1, nv)
    A_, B_ = D['A'] - 0.0015, D['B'] - 0.0015       # root a little inside the crown wall
    x0 = A_ * np.sin(psis); y0 = -B_ * np.cos(psis)
    nx = np.sin(psis) / A_; ny = -np.cos(psis) / B_
    nl = np.hypot(nx, ny); nx /= nl; ny /= nl
    f = B['fwd']                                                           # the visor edges run mostly forward, not sideways
    nx, ny = nx * (1 - f), ny * (1 - f) - f
    nl = np.hypot(nx, ny); nx /= nl; ny /= nl
    L = B['L0'] * np.clip(np.cos(psis / B['psi'] * math.pi / 2 * .93), 0, 1) ** .36 + 0.006
    d = v[None, :] * L[:, None]                                        # distance from the root along the outward direction
    p = np.radians(B['p0'] + B['p1'] * np.sin(psis) ** 2)[:, None]
    X = x0[:, None] + nx[:, None] * d
    Y = y0[:, None] + ny[:, None] * d
    Z = B['z0'] - d * np.tan(p) - B['curl'] * d * d
    P = np.stack([X, Y, Z], -1)
    fields = dict(d_edge=(1 - v[None, :]) * L[:, None], d_root=d, psi=np.broadcast_to(psis[:, None], (nu, nv)),
                  bx=X, by=Y)
    # grid orientation: u = psi increases toward +x, v outward (-y): du x dv points DOWN; flip u so the normal points up (the visible top)
    P = P[::-1]; fields = {k: np.asarray(f)[::-1] for k, f in fields.items()}
    ob = G.thick_grid('Brim', P, B['T'], edge_steps=5, rim_u=(True, True), rim_v=(False, True), fields=fields)
    return ob


def build_band(D, V, C):
    Bd = D['band']
    delta = D['notch_half'] + math.radians(3.0)
    nu = 44; nv = 5
    phis = np.linspace(math.pi + delta, 3 * math.pi - delta, nu)
    zs = np.linspace(-Bd['drop'], Bd['h'], nv)
    PH = np.broadcast_to(phis[:, None], (nu, nv)); Z = np.broadcast_to(zs[None, :], (nu, nv))
    Zc = np.clip(Z, 0, None)
    pts = C.radial_at(PH, np.maximum(Zc, 1e-6))
    # radial inset: the band's visible face sits T + band thickness + 0.2 mm inside the crown outer surface; z follows the requested height
    sc_x = (D['A'] - D['T'] - Bd['T'] - 0.0003) / D['A']; sc_y = (D['B'] - D['T'] - Bd['T'] - 0.0003) / D['B']
    P = pts.copy(); P[..., 0] *= sc_x; P[..., 1] *= sc_y; P[..., 2] = Z
    # orientation: u increases counter-clockwise seen from the top; du x dv = outward -> flip u so the visible face is the inner one
    P = P[::-1]
    fields = dict(phi=wrap_pi(PH)[::-1], zc=Z[::-1], d_seam=np.full((nu, nv), 1.0), w=np.zeros((nu, nv)))
    # the thickness goes outward (toward the crown): the grid normal must point to the head -> grid_normals of the flipped grid points inward already
    ob = G.thick_grid('Sweatband', P, Bd['T'], edge_steps=4, rim_u=(True, True), rim_v=(True, True), fields=fields)
    return ob


def build_button(D, V, C):
    b = D['button']
    r, h = b['r'], b['h']
    prof = [(r * .98, 0.0), (r * 1.0, h * .22), (r * .93, h * .55), (r * .70, h * .86), (r * .38, h * 1.02), (0.0, h * 1.06)]
    ob = G.lathe('Button', prof, sides=24, closed_bottom=True)
    P = G.positions(ob); P[:, 2] += D['H'] - 0.0012
    G.set_positions(ob, P)
    return ob


def build_piping(D, V, C, which=(1, 2, 4, 5)):
    objs = []
    for k in which:
        phi = k * 2 * math.pi / D['panels']
        zs = np.linspace(0.0, D['H'] * .985, 30)
        pts = np.array([C.radial_at(phi, max(z, 1e-6)) for z in zs])
        # drop the groove: pipe sits in the crease, half proud
        n = G.unit(pts - np.array([0, 0, 0]) * 0)
        pts = pts + np.array([np.sin(phi) * 0, 0, 0])
        # outward direction in plan
        out = np.stack([np.sin(phi) * np.ones(len(zs)), -np.cos(phi) * np.ones(len(zs)), np.zeros(len(zs))], -1)
        pts = pts + out * 0.0003 - np.array([0, 0, 0])
        rad = 0.0014 * (1 - 0.55 * (zs / D['H']) ** 4)
        fld = dict(phi=np.full(len(zs), wrap_pi(phi)), zc=zs, d_seam=np.zeros(len(zs)), w=np.zeros(len(zs)))
        objs.append(G.tube(f'Piping_{k}', pts, rad, sides=6, caps=True, fields=fld))
    return objs


def build_strap(D, V, C):
    S = D['strap']
    delta = D['notch_half'] + math.radians(11)
    nu, nv = 30, 5
    phis = np.linspace(math.pi - delta, math.pi + delta, nu)
    zs = np.linspace(S['z'] - S['w'] / 2, S['z'] + S['w'] / 2, nv)
    PH = np.broadcast_to(phis[:, None], (nu, nv)); Z = np.broadcast_to(zs[None, :], (nu, nv))
    pts = C.radial_at(PH, np.maximum(Z, 1e-6))
    sc_x = (D['A'] - 0.0006) / D['A']; sc_y = (D['B'] - 0.0006) / D['B']
    P = pts.copy(); P[..., 0] *= sc_x; P[..., 1] *= sc_y; P[..., 2] = Z
    # du x dv: u along phi (counter-clockwise) -> outward, as the crown
    sx = (phis - math.pi)[:, None] * 0.5 * (D['A'] + D['B']) * np.ones((1, nv)); sy = (Z - S['z'])
    fields = dict(phi=wrap_pi(PH), zc=Z, d_seam=np.full((nu, nv), 1.0), w=np.zeros((nu, nv)), sx=sx, sy=sy)
    ob = G.thick_grid('Strap', P, S['T'], edge_steps=3, rim_u=(True, True), rim_v=(True, True), fields=fields)
    return ob


def build_buckle(D, V, C):
    S = D['strap']
    phi = math.pi + 0.20
    ctr = C.radial_at(phi, S['z'])
    ctr = ctr * np.array([(D['A'] + 0.0012) / D['A'], (D['B'] + 0.0012) / D['B'], 1])
    out = np.array([np.sin(phi) / D['A'], -np.cos(phi) / D['B'], 0]); out = G.unit(out)
    tan = np.array([np.cos(phi), np.sin(phi), 0]); tan = G.unit(tan)
    up = np.array([0, 0, 1.])
    w, h = 0.0125, S['w'] / 2 + 0.0030                                 # half-width along the strap, half-height across it
    rr = 0.00115

    def rect(wd, hd, n=8):
        pts = []
        for corner, (sx, sy) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
            for a in np.linspace(corner * math.pi / 2 + (0 if corner else 0), (corner + 1) * math.pi / 2, n):
                pass
        # rounded rectangle via superellipse
        th = np.linspace(0, 2 * math.pi, 40, endpoint=False)
        c, s = np.cos(th), np.sin(th)
        x = wd * np.sign(c) * np.abs(c) ** .22; y = hd * np.sign(s) * np.abs(s) ** .22
        return x, y
    x, y = rect(w, h)
    # frame bulges slightly off the strap's skin
    path = np.array([ctr + out * 0.0012 + tan * xi + up * yi for xi, yi in zip(x, y)])
    frame = G.tube('Buckle_Frame', path, rr, sides=6, closed=True)
    bar = np.array([ctr + out * 0.0012 + tan * (0.0016 * 0) + up * yy for yy in np.linspace(-h, h, 10)])
    bar = bar + tan * 0.0
    barobj = G.tube('Buckle_Bar', bar, rr * .85, sides=6, closed=False)
    return [frame, barobj]


def build_patch(D, V, C):
    """golf cap: the white woven front patch, a thin proud rectangle that follows the crown."""
    nu, nv = 29, 19
    pw, ph = 0.108, 0.056
    z_c = 0.034
    xs = np.linspace(-pw / 2, pw / 2, nu); zs = np.linspace(z_c - ph / 2, z_c + ph / 2, nv)
    X, Z = np.meshgrid(xs, zs, indexing='ij')
    # project onto the crown by arc length on the front: phi from x, height from z
    rad = lambda z: .5 * (D['A'] + D['B']) * np.cos(C.uz(np.maximum(z, 1e-6))) ** D['e']
    phi = X / np.maximum(rad(Z), 1e-3)
    P = C.radial_at(phi, Z)
    n = G.grid_normals(P)
    # orientation: u = +x (toward hero's left), v = up; du x dv = (1,0,0)x(0,0,1) = (0,-1,0) = outward at the front: ok
    P = P + n * 0.0027
    fields = dict(phi=phi, zc=Z, d_seam=np.full((nu, nv), 1.0), w=np.zeros((nu, nv)), px=X, py=Z - z_c)
    ob = G.thick_grid('Patch', P, 0.0020, edge_steps=3, rim_u=(True, True), rim_v=(True, True), fields=fields)
    return ob


def set_kind(ob, kind, fabric, sub=0):
    G.set_face_attr(ob, 'kind', kind)
    G.set_face_attr(ob, 'fabric', fabric)
    G.set_face_attr(ob, 'sub', sub)


def preview_materials(V):
    c = V['colours']
    base = A.material('PV_base', A.srgb(c['base']), .72, .35)
    trim = A.material('PV_trim', A.srgb(c['trim']), .68, .30)
    metal = A.material('PV_metal', A.srgb('C9CCD1'), .28, 0, metallic=.95)
    return base, trim, metal


def build(variant, H=None):
    A.reset()
    D = dict(BASE); V = VARIANTS[variant]
    if H: D['H'] = H
    crown, C = build_crown(D, V)
    set_kind(crown, K_CROWN, FAB['twill'])
    parts = [crown]
    brim = brim_geometry(D, V, C); set_kind(brim, K_BRIM, FAB['twill']); parts.append(brim)
    band = build_band(D, V, C); set_kind(band, K_BAND, FAB['rib']); parts.append(band)
    btn = build_button(D, V, C); set_kind(btn, K_BUTTON, FAB['twill']); parts.append(btn)
    strap = build_strap(D, V, C); set_kind(strap, K_STRAP, FAB['rib'] if V['strap_kind'] == 'webbing' else FAB['plastic']); parts.append(strap)
    metal = []
    if V['piping']:
        for p in build_piping(D, V, C): set_kind(p, K_PIPING, FAB['pique']); parts.append(p)
    if V['strap_kind'] == 'webbing':
        for p in build_buckle(D, V, C): set_kind(p, K_BUCKLE, FAB['plastic'], sub=1); parts.append(p); metal.append(p)
    if V['patch']:
        pt = build_patch(D, V, C); set_kind(pt, K_PATCH, FAB['twill']); parts.append(pt)
    if V['strap_kind'] == 'snap':
        punch_strap_holes(strap, D, C)
    return parts, D, C, V


def punch_strap_holes(strap, D, C):
    S = D['strap']
    n = 6; pitch = 0.0105
    cut = []
    for k in range(n):
        s = (k - (n - 1) / 2) * pitch
        phi = math.pi + s / (0.5 * (D['A'] + D['B']))
        p = C.radial_at(phi, S['z'])
        out = G.unit(np.array([np.sin(phi) / D['A'], -np.cos(phi) / D['B'], 0]))
        bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=0.00155, depth=0.012, location=tuple(p))
        cy = bpy.context.active_object
        z = np.array([0, 0, 1.]); ax = np.cross(z, out); ang = math.acos(np.clip(np.dot(z, out), -1, 1))
        if np.linalg.norm(ax) > 1e-9:
            cy.rotation_mode = 'AXIS_ANGLE'; cy.rotation_axis_angle = (ang, *G.unit(ax))
        cut.append(cy)
    for cy in cut:
        G.boolean_diff(strap, cy, solver='EXACT')
    G.fix_normals(strap)


def finish(parts, variant, outpath):
    base, trim, metal = preview_materials(VARIANTS[variant])
    trim_kinds = {K_BRIM, K_PIPING, K_BUTTON, K_STRAP} if variant == 'tennis' else {K_PATCH}
    for ob in parts:
        k = int(np.asarray([p.value for p in ob.data.attributes['kind'].data])[0]) if len(ob.data.polygons) else 0
        ob.data.materials.clear()
        ob.data.materials.append(metal if k == K_BUCKLE else trim if k in trim_kinds else base)
    return parts


def show(variant, parts, outdir, prefix, with_maps=None):
    G_ = G
    A.studio(res=(900, 900), bg=(.34, .35, .37))
    sc = bpy.context.scene
    views = {'front': (0, 10), 'side': (90, 8), 'back': (180, 10), '34': (38, 20), 'top': (0, 88), 'under': (0, -75), 'back34': (150, 18), 'side_r': (-90, 8)}
    for name, (az, el) in views.items():
        cam = A.camera((0, -0.020, 0.040), az, el, 0.95, lens=75)
        if name == 'top': cam.rotation_euler[2] += 0
        A.render(os.path.join(outdir, f'{prefix}_{name}.png'))


if __name__ == '__main__':
    args = A.args_after_dashes()
    variant, out = args[0], args[1]
    parts, D, C, V = build(variant)
    finish(parts, variant, out)
    for p in parts:
        print(p.name, G.mesh_stats(p))
    bpy.ops.wm.save_as_mainfile(filepath=out)
    if len(args) > 2:
        show(variant, parts, args[2], variant + '_cap')
