"""The Hero's headwear (cap, visor, sweatband), built to sit over the hair from hero_hair.py.

Each hat is a shell that follows the head's envelope (the bare head, or the hair on it pressed a little under the hat),
so it rests on the hair instead of cutting into it and never floats off a bare head:

  * the mesh is built once per fit with the same topology, so the bare-head fit is the base shape and each haircut's fit
    is a blend shape `OnHair_<cut>` on it (the game turns one on);
  * the hair under a hat is pressed (`Under_<hat>` on the haircut, made here too) by the same coverage function the hat
    was fitted to, and the coverage is also written as that hat's hold channel in the hair's vertex colour (G visor,
    B cap, A sweatband) so the shader does not sway held hair through the hat.

Materials are named by role: Cos_Hat (the hat's colour, from the look) and Cos_Trim (its bill and stripes, the same
colour a shade darker); HeroGolfer picks them by name.
"""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

import hero_head as H
import hero_hair as HH

CENTER = H.CENTER
HATS = ("Visor", "Cap", "Sweatband")     # the order of their hold channels G, B, A
COMPRESS = {"Visor": 0.72, "Cap": 0.22, "Sweatband": 0.72}    # how much of the hair's thickness is left under each hat
GAP = 0.0025


def _smooth(t):
    return HH._smooth(t)


def _dir(phi_deg, z):
    """Direction from CENTER toward azimuth phi (degrees from straight ahead, + toward +X) at height z, seen from the axis."""
    a = math.radians(phi_deg)
    return Vector((math.sin(a) * 0.16, -math.cos(a) * 0.16, z - CENTER.z)).normalized()


def _dir_e(phi_deg, el):
    a = math.radians(phi_deg)
    return Vector((math.sin(a) * math.cos(el), -math.cos(a) * math.cos(el), math.sin(el)))


class Fit:
    """The surface a hat rests on, in any direction: the bare head, or the head with a haircut pressed under `cover`."""

    def __init__(self, head, hair_tree=None, cover=None, compress=0.45, spread=0.03):
        self.head, self.hair_tree, self.cover, self.compress, self.spread = head, hair_tree, cover, compress, spread
        self.cache = {}

    def hair_outer(self, d):
        if self.hair_tree is None: return None
        hit = self.hair_tree.ray_cast(CENTER + d * 0.7, -d, 0.7)
        return 0.7 - hit[3] if hit[0] is not None else None

    def __call__(self, d):
        key = (round(d.x, 4), round(d.y, 4), round(d.z, 4))
        if key in self.cache: return self.cache[key]
        self.head.bridge = self.spread
        r = self.head.env(d, self.spread)
        if self.hair_tree is not None:
            # the hair's outer reach here (the most within a few degrees: a hat clears its bumps), pressed
            base = self.head.radial(d) or r
            a = d.orthogonal().normalized(); b = d.cross(a)
            outer = self.hair_outer(d) or 0.0
            for k in range(8):
                t = k * math.pi / 4
                o = self.hair_outer((d + (a * math.cos(t) + b * math.sin(t)) * 0.05).normalized())
                if o is not None and o > outer: outer = o
            # a wide, soft average of how thick the hair is round here, so the hat is smooth over lumps, but never
            # under the local bumps it must clear (the pressed hair is thin, so the two are close)
            wide = []
            for k in range(8):
                t = k * math.pi / 4
                o = self.hair_outer((d + (a * math.cos(t) + b * math.sin(t)) * 0.11).normalized())
                wide.append(max(0.0, (o or 0.0) - base))
            avg = (sum(wide) + max(0.0, outer - base)) / 9.0
            thick = max(0.0, outer - base)
            thick = max(avg, thick * 0.9)
            c = self.cover(d, base) if self.cover else 0.0
            r = max(r, base + thick * (1 - (1 - self.compress) * c))
        self.cache[key] = r
        return r


def _grid(bm, rows, cols, fn, closed=False):
    """A grid of vertices fn(row, col) -> Vector; returns the rows of vertices."""
    return [[bm.verts.new(fn(i, j)) for j in range(cols)] for i in range(rows)]


def _quads(bm, g, closed=False, flip=False, mat=0, rows=None):
    n = len(g); m = len(g[0])
    faces = []
    for i in range(n - 1):
        for j in range(m if closed else m - 1):
            a, b = g[i][j], g[i][(j + 1) % m]
            c, d = g[i + 1][(j + 1) % m], g[i + 1][j]
            f = bm.faces.new((d, c, b, a) if flip else (a, b, c, d))
            f.material_index = mat if rows is None else rows(i)
            faces.append(f)
    return faces


def _rim(bm, top, bot, closed, mat=0):
    """Close the gap between two identical grids' `edge` lists of vertices (top -> bot)."""
    m = len(top)
    for j in range(m if closed else m - 1):
        a, b = top[j], top[(j + 1) % m]
        c, d = bot[(j + 1) % m], bot[j]
        f = bm.faces.new((a, b, c, d)); f.material_index = mat


# --------------------------------------------------------------------------------------------- the hats' own lines

def _table(rows):
    pts = sorted(rows)
    pts = [(-a, z) for a, z in reversed(pts) if a] + pts
    return HH.hem_fn([(a, z) for a, z in pts])


BAND = {   # z of the band's centre at each angle from straight ahead, its height, its thickness
    "Visor": (_table([(0, 1.508), (40, 1.503), (70, 1.492), (100, 1.478), (130, 1.468), (180, 1.462)]), 0.040, 0.0075),
    "Sweatband": (_table([(0, 1.498), (40, 1.496), (70, 1.488), (100, 1.476), (130, 1.47), (180, 1.466)]), 0.054, 0.012),
}
CAP_EDGE = _table([(0, 1.497), (40, 1.492), (70, 1.478), (100, 1.458), (130, 1.447), (180, 1.444)])
CAP_T = 0.0065


def cover_fn(hat):
    """How far the hat presses the hair at direction d (0..1): under a cap's crown, or within a band's height."""
    if hat == "Cap":
        def f(d, base):
            z = CENTER.z + d.z * base
            phi = math.degrees(math.atan2(d.x, -d.y))
            return _smooth((z - (CAP_EDGE(phi) - 0.02)) / 0.035)
        return f
    zc, h, _ = BAND[hat]
    def f(d, base):
        z = CENTER.z + d.z * base
        phi = math.degrees(math.atan2(d.x, -d.y))
        c = zc(phi)
        return _smooth((z - (c - h / 2 - 0.036)) / 0.036) * _smooth(((c + h / 2 + 0.036) - z) / 0.036)
    return f


def _band(bm, fit, hat, segs=72):
    zc, h, t = BAND[hat]
    # rounded rectangle cross-section, inner side first: (radial offset 0 = inner surface, +t outer; dz)
    prof = []
    rr = min(t, h * 0.25) * 0.9
    prof += [(0.0, -h / 2 + rr), (rr * 0.35, -h / 2 + rr * 0.15), (rr, -h / 2), (t - rr, -h / 2), (t - rr * 0.35, -h / 2 + rr * 0.15), (t, -h / 2 + rr)]
    n_out = 5
    for k in range(1, n_out):
        prof.append((t, -h / 2 + rr + (h - 2 * rr) * k / n_out))
    prof += [(t, h / 2 - rr), (t - rr * 0.35, h / 2 - rr * 0.15), (t - rr, h / 2), (rr, h / 2), (rr * 0.35, h / 2 - rr * 0.15), (0.0, h / 2 - rr)]
    m = len(prof)
    # the band's inner radius all round, at each row of its section: smoothed along the band so it is a clean ring
    # over lumpy hair, and never lower than the hair needs (within 4 mm)
    dirs = [[_dir(360.0 * i / segs - 180.0, zc(360.0 * i / segs - 180.0) + dz) for _, dz in prof] for i in range(segs)]
    raw = np.array([[fit(dirs[i][k]) for k in range(m)] for i in range(segs)])
    soft = raw.copy()
    for _ in range(5):     # round the ring, wide: the hair's lumps must not crinkle the band's edges
        soft = (soft + np.roll(soft, 1, 0) + np.roll(soft, -1, 0) + soft * 2 + np.roll(soft, 2, 0) + np.roll(soft, -2, 0)) / 7.0
    for _ in range(3):     # and up the section: every row takes its neighbours' radius, so it stays one band
        soft = (np.roll(soft, 1, 1) + np.roll(soft, -1, 1) + soft * 2) / 4.0
    radius = np.maximum(soft, raw - 0.0025)
    grid = []
    for i in range(segs):
        grid.append([bm.verts.new(CENTER + dirs[i][k] * (float(radius[i][k]) + GAP + prof[k][0])) for k in range(m)])
    # the profile is a closed loop round the band's section; the band a closed loop round the head
    for i in range(segs):
        for k in range(m):
            a, b = grid[i][k], grid[i][(k + 1) % m]
            c, d = grid[(i + 1) % segs][(k + 1) % m], grid[(i + 1) % segs][k]
            f = bm.faces.new((a, d, c, b))
            # the sweatband's two stripes: the outer rows a fifth up and a fifth down, in the trim colour
            f.material_index = 1 if (hat == "Sweatband" and k in (6, 8)) else 0
    return grid, prof


def _bill(bm, fit, base_z, base_r_off, length, width_deg=52.0, pitch=0.24, droop=0.02, nu=15, nv=6, thick=0.0075, mat=1):
    """The bill: a curved plate from the hat's lower front edge, out and down, rounded in front."""
    top = []; bot = []
    # where the bill leaves the hat: its base radius, smoothed along the bill (a bill is a clean plate, not a copy of the hair)
    raw = np.array([fit(_dir(width_deg * (2.0 * i / (nu - 1) - 1.0), base_z(width_deg * (2.0 * i / (nu - 1) - 1.0)))) for i in range(nu)])
    soft = raw.copy()
    for _ in range(12):
        soft = np.concatenate([[soft[0]], (soft[:-2] + 2 * soft[1:-1] + soft[2:]) / 4.0, [soft[-1]]])
    base_r = np.maximum(soft, raw - 0.0015)
    for i in range(nu):
        u = 2.0 * i / (nu - 1) - 1.0
        phi = u * width_deg
        rowt, rowb = [], []
        for j in range(nv):
            v = j / (nv - 1)
            zb = base_z(phi)
            R = float(base_r[i]) + GAP + base_r_off
            L = length * math.sqrt(max(0.0, 1 - 0.9 * u * u))
            a = math.radians(phi)
            n = Vector((math.sin(a), -math.cos(a), 0.0))
            front = Vector((0.0, -1.0, 0.0))
            dirv = (n * (1 - 0.55 * v) + front * (0.55 * v)).normalized()
            p = Vector((CENTER.x, CENTER.y, zb)) + n * R + dirv * (v * L)
            p.z -= v * L * pitch + droop * u * u * v
            rowt.append(bm.verts.new(p + Vector((0, 0, thick / 2))))
            rowb.append(bm.verts.new(p - Vector((0, 0, thick / 2))))
        top.append(rowt); bot.append(rowb)
    for i in range(nu - 1):
        for j in range(nv - 1):
            f = bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j])); f.material_index = mat
            f = bm.faces.new((bot[i][j], bot[i + 1][j], bot[i + 1][j + 1], bot[i][j + 1])); f.material_index = mat
    # the rim: both sides and the front edge
    for i in range(nu - 1):
        f = bm.faces.new((top[i][nv - 1], bot[i][nv - 1], bot[i + 1][nv - 1], top[i + 1][nv - 1])); f.material_index = mat
    for j in range(nv - 1):
        f = bm.faces.new((top[0][j], top[0][j + 1], bot[0][j + 1], bot[0][j])); f.material_index = mat
        f = bm.faces.new((top[nu - 1][j], bot[nu - 1][j], bot[nu - 1][j + 1], top[nu - 1][j + 1])); f.material_index = mat
    for i in range(nu - 1):   # the back edge, tucked against the head
        f = bm.faces.new((top[i][0], top[i + 1][0], bot[i + 1][0], bot[i][0])); f.material_index = mat


def _crown(bm, fit, segs=72, rows=14):
    """A cap's dome from its lower edge to the top, with a thickness, six panel seams and a button."""
    outer, inner = [], []
    # the dome's inner radius everywhere, smoothed round the cap and up it (so its edge is a clean line over lumpy
    # hair), never lower than the hair needs (within 1.5 mm)
    dirs = []
    for i in range(segs):
        phi = 360.0 * i / segs - 180.0
        e0 = math.asin(max(-1.0, min(1.0, _dir(phi, CAP_EDGE(phi)).z)))
        dirs.append([_dir_e(phi, e0 + (math.pi / 2 - e0) * ((j / (rows - 1)) ** 0.85)) for j in range(rows)])
    raw = np.array([[fit(dirs[i][j]) for j in range(rows)] for i in range(segs)])
    soft = raw.copy()
    for _ in range(2):
        soft = (soft + np.roll(soft, 1, 0) + np.roll(soft, -1, 0) + soft * 2 + np.roll(soft, 2, 0) + np.roll(soft, -2, 0)) / 7.0
    radius = np.maximum(soft, raw - 0.0015)
    for i in range(segs):
        phi = 360.0 * i / segs - 180.0
        o, n_ = [], []
        for j in range(rows):
            v = j / (rows - 1)
            d = dirs[i][j]
            R = float(radius[i][j]) + GAP
            seam = 0.0
            # six panels: raised seams from the button to the edge (angles 0, 60, ...), fading into the button
            ph = (phi % 60.0); dist = min(ph, 60 - ph)
            seam = math.exp(-(dist / 2.6) ** 2) * (1.0 - _smooth((v - 0.85) / 0.15)) * 0.0035
            o.append(bm.verts.new(CENTER + d * (R + CAP_T + seam)))
            n_.append(bm.verts.new(CENTER + d * R))
        outer.append(o); inner.append(n_)
    for i in range(segs):
        i2 = (i + 1) % segs
        for j in range(rows - 1):
            f = bm.faces.new((outer[i][j], outer[i2][j], outer[i2][j + 1], outer[i][j + 1])); f.material_index = 0
            f = bm.faces.new((inner[i][j], inner[i][j + 1], inner[i2][j + 1], inner[i2][j])); f.material_index = 0
        f = bm.faces.new((outer[i][0], inner[i][0], inner[i2][0], outer[i2][0])); f.material_index = 0
    # button
    top = CENTER + Vector((0, 0, 1)) * (fit(Vector((0, 0, 1))) + GAP + CAP_T + 0.004)
    hub = [bm.verts.new(top + Vector((math.cos(a) * 0.011, math.sin(a) * 0.011, -0.003))) for a in np.linspace(0, 2 * math.pi, 10, endpoint=False)]
    tip = bm.verts.new(top + Vector((0, 0, 0.006)))
    for k in range(10):
        f = bm.faces.new((tip, hub[k], hub[(k + 1) % 10])); f.material_index = 1


def orient_outward(bm):
    """Point every piece's faces out of it. recalc_face_normals makes each piece consistent but guesses which way is
    out, and on a thin ring (a band) it guesses in, which the game's back-face culling turns into a band you see
    only from inside. So: consistent first, then by the sign of the piece's volume (taken about its own centre)."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    seen = set()
    for f0 in list(bm.faces):
        if f0 in seen: continue
        comp, stack = [], [f0]
        seen.add(f0)
        while stack:
            f = stack.pop(); comp.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g); stack.append(g)
        verts = {v for f in comp for v in f.verts}
        centre = sum((v.co for v in verts), Vector()) / len(verts)
        vol = 0.0
        for f in comp:
            vs = [v.co - centre for v in f.verts]
            for i in range(1, len(vs) - 1): vol += vs[0].dot(vs[i].cross(vs[i + 1]))
        if vol < 0: bmesh.ops.reverse_faces(bm, faces=comp)


def build_hat(hat, fit):
    """The hat as a bmesh (world space) for one fit. Same topology whatever the fit."""
    bm = bmesh.new()
    if hat == "Cap":
        _crown(bm, fit)
        _bill(bm, fit, lambda phi: CAP_EDGE(phi) + 0.008, CAP_T - 0.001, 0.105, pitch=0.30, width_deg=46.0)
    elif hat == "Visor":
        zc, h, t = BAND["Visor"]
        _band(bm, fit, "Visor")
        _bill(bm, fit, lambda phi: zc(phi) - h * 0.30, t - 0.001, 0.115, pitch=0.22, width_deg=46.0)
    else:
        _band(bm, fit, "Sweatband")
    orient_outward(bm)
    return bm


def bake_hat(hat, head, arm, cuts):
    """The hat object: base shape on the bare head, a shape key OnHair_<cut> over each haircut in `cuts`
    (name -> hair object). Returns the object."""
    bm = build_hat(hat, Fit(head))
    me = bpy.data.meshes.new("Hat_" + hat)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    obj = bpy.data.objects.new("Hat_" + hat, me)
    bpy.context.scene.collection.objects.link(obj)
    for name in ("Cos_Hat", "Cos_Trim"):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        obj.data.materials.append(m)
    obj.shape_key_add(name="Basis", from_mix=False)
    for name, hair in cuts.items():
        tree = _tree_of(hair)
        fit = Fit(head, tree, cover_fn(hat), compress=COMPRESS[hat])
        pos = [v.co.copy() for v in build_hat(hat, fit).verts] if False else _positions(hat, fit)
        key = obj.shape_key_add(name="OnHair_" + name, from_mix=False)
        for d, p in zip(key.data, pos): d.co = p
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    g = obj.vertex_groups.new(name="Head")
    g.add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    mod = obj.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
    return obj


def _positions(hat, fit):
    bm = build_hat(hat, fit)
    pos = [v.co.copy() for v in bm.verts]
    bm.free()
    return pos


def _tree_of(obj):
    mw = obj.matrix_world
    verts = [mw @ v.co for v in obj.data.vertices]
    return BVHTree.FromPolygons(verts, [tuple(p.vertices) for p in obj.data.polygons])


def press_hair(hair, head):
    """Give `hair` its Under_<hat> shape keys (the hair pressed under each hat) and write each hat's coverage as the
    hold channel G, B, A of its vertex colour (R is the flex). Returns the hold arrays."""
    me = hair.data
    if not me.shape_keys: hair.shape_key_add(name="Basis", from_mix=False)
    col = me.color_attributes.get("Col")
    out = {}
    for k, hat in enumerate(HATS):
        cover = cover_fn(hat)
        cov = np.zeros(len(me.vertices))
        pos = []
        for v in me.vertices:
            p = hair.matrix_world @ v.co
            r = (p - CENTER).length
            d = (p - CENTER) / r
            base = head.radial(d)
            c = cover(d, base) if base is not None else 0.0
            cov[v.index] = c
            if base is not None and r > base:
                r = base + (r - base) * (1 - (1 - COMPRESS[hat]) * c)
            pos.append(CENTER + d * r)
        key = hair.shape_key_add(name="Under_" + hat, from_mix=False)
        inv = hair.matrix_world.inverted()
        for d_, p in zip(key.data, pos): d_.co = inv @ p
        out[hat] = cov
    if col is not None:
        for v in me.vertices:
            c = list(col.data[v.index].color)
            for k, hat in enumerate(HATS): c[1 + k] = float(out[hat][v.index])
            col.data[v.index].color = c
    return out
