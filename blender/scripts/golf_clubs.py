"""The four golf clubs, modelled for the game: a carbon-crowned driver, a chrome cavity-back iron,
a satin wedge and a blade putter, each with a tapered shaft, ferrule and rubber grip.

Built in the space of the studio's club roots (SportsLibrary/Blender/sports-animation-studio-v4.blend,
"<Gender> V4 <Club> Golf_<Club>_ROOT"), which adnan_golfer_export.py bakes into the Club bone, so
they swing exactly where the studio's simpler clubs did:

  * the grip runs z = -0.16 .. +0.07, the butt at the top; the shaft hangs straight down -Z;
  * the head's heel is on the shaft axis, the toe toward +X, the face toward -Y; its sole is the
    studio's (SOLE below), square to the shaft;
  * loft opens the face toward +Z, the shaft's way up.

    build(club) -> bpy.types.Object   (mesh in that space, materials named "CLUB <finish>")

The game gives the CLUB materials real metal (GolferView.ClubMaterial); their viewport colours
here are only for Blender renders (club_renders.py).
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix

CLUBS = ("Driver", "Iron", "Wedge", "Putter")
# The studio's sole and ferrule heights, per club (root space, before the root's own scale).
SOLE = {"Driver": -1.084, "Iron": -0.946, "Wedge": -0.883, "Putter": -0.834}
FERRULE_TOP = {"Driver": -0.995, "Iron": -0.865, "Wedge": -0.805, "Putter": -0.765}
LOFT = {"Driver": 10.5, "Iron": 30.0, "Wedge": 56.0, "Putter": 3.0}

# name: (base colour, metallic, roughness)
FINISHES = {
    "CLUB chrome":   ((0.86, 0.87, 0.89), 1.0, 0.12),
    "CLUB satin":    ((0.72, 0.73, 0.75), 1.0, 0.35),
    "CLUB gunmetal": ((0.22, 0.23, 0.25), 1.0, 0.30),
    "CLUB carbon":   ((0.035, 0.037, 0.042), 0.0, 0.38),
    "CLUB black":    ((0.02, 0.02, 0.022), 0.0, 0.55),
    "CLUB grip":     ((0.03, 0.03, 0.032), 0.0, 0.85),
    "CLUB accent":   ((0.05, 0.62, 0.62), 0.0, 0.40),   # Adnan's sea teal
    "CLUB white":    ((0.92, 0.92, 0.90), 0.0, 0.50),
    "CLUB groove":   ((0.08, 0.08, 0.09), 1.0, 0.45),
}


def material(name):
    m = bpy.data.materials.get(name)
    if m: return m
    col, metal, rough = FINISHES[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*col, 1)
    return m


class Builder:
    """One bmesh with a material list; parts are added in root space."""
    def __init__(self):
        self.bm = bmesh.new(); self.mats = []

    def mat(self, name):
        if name not in self.mats: self.mats.append(name)
        return self.mats.index(name)

    def ring_tube(self, rings, seg, name, cap_top=True, cap_bottom=True):
        """rings: [(z, radius, (cx, cy))] bottom to top — a tube of circles."""
        mi = self.mat(name); bm = self.bm
        loops = []
        for z, r, (cx, cy) in rings:
            loops.append([bm.verts.new((cx + r * math.cos(2 * math.pi * i / seg), cy + r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
        for a, b in zip(loops, loops[1:]):
            for i in range(seg):
                f = bm.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i])); f.material_index = mi; f.smooth = True
        if cap_bottom:
            f = bm.faces.new(list(reversed(loops[0]))); f.material_index = mi
        if cap_top:
            f = bm.faces.new(loops[-1]); f.material_index = mi
        return loops

    def grid_solid(self, rows, name_of, closed_u=True, cap=True):
        """rows: list of vertex-coordinate loops (same length); name_of(i_row, j) -> material for
        the quad starting there. Caps the first and last loop: one face, or cap="fan" to a point
        in the middle (for curved caps like a crown, which one face can't follow)."""
        bm = self.bm
        vs = [[bm.verts.new(p) for p in row] for row in rows]
        n = len(rows[0])
        for i in range(len(vs) - 1):
            for j in range(n if closed_u else n - 1):
                f = bm.faces.new((vs[i][j], vs[i][(j + 1) % n], vs[i + 1][(j + 1) % n], vs[i + 1][j]))
                f.material_index = self.mat(name_of(i, j)); f.smooth = True
        if cap == "fan":
            for loop, i_row, flip in ((vs[0], 0, True), (vs[-1], len(vs) - 2, False)):
                mid = bm.verts.new(sum((v.co for v in loop), Vector()) / n)
                for j in range(n):
                    a, b = loop[j], loop[(j + 1) % n]
                    f = bm.faces.new((b, a, mid) if flip else (a, b, mid))
                    f.material_index = self.mat(name_of(i_row, j)); f.smooth = True
        elif cap:
            f = bm.faces.new(list(reversed(vs[0]))); f.material_index = self.mat(name_of(0, 0))
            f = bm.faces.new(vs[-1]); f.material_index = self.mat(name_of(len(vs) - 2, 0))
        return vs

    def box(self, center, size, name, rot=None):
        mi = self.mat(name)
        m = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = m["verts"]
        S = Matrix.Diagonal((*size, 1))
        R = rot.to_4x4() if rot else Matrix.Identity(4)
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(center) @ R @ S, verts=verts)
        for f in {f for v in verts for f in v.link_faces}: f.material_index = mi

    def finish(self, name):
        me = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(me); self.bm.free()
        for n in self.mats: me.materials.append(material(n))
        ob = bpy.data.objects.new(name, me)
        # smooth where it's curved, crisp at the edges
        me.set_sharp_from_angle(angle=math.radians(40)) if hasattr(me, "set_sharp_from_angle") else None
        return ob


def shaft_and_grip(b, club):
    """Grip (tapered rubber with a teal collar), shaft (graphite for the driver, stepped steel for
    the rest) and ferrule down to the hosel."""
    seg = 24
    grip = [(-0.160, 0.0112), (-0.150, 0.0118), (-0.100, 0.0126), (-0.030, 0.0136), (0.040, 0.0144), (0.066, 0.0147), (0.070, 0.0140)]
    b.ring_tube([(z, r, (0, 0)) for z, r in grip], seg, "CLUB grip")
    b.ring_tube([(-0.146, 0.0122, (0, 0)), (-0.138, 0.0124, (0, 0))], seg, "CLUB accent", cap_top=False, cap_bottom=False)
    b.ring_tube([(0.0705, 0.0128, (0, 0)), (0.0715, 0.0100, (0, 0))], seg, "CLUB accent")   # butt cap logo ring
    ferrule_top = FERRULE_TOP[club]
    top = -0.155
    if club == "Driver":
        b.ring_tube([(ferrule_top, 0.0045, (0, 0)), (top, 0.0068, (0, 0))], 16, "CLUB carbon", cap_top=False, cap_bottom=False)
        mid = ferrule_top + (top - ferrule_top) * 0.62
        b.ring_tube([(mid, 0.0062, (0, 0)), (mid + 0.07, 0.0065, (0, 0))], 16, "CLUB accent", cap_top=False, cap_bottom=False)
    else:
        rings, n = [], 7
        for k in range(n + 1):
            z = ferrule_top + (top - ferrule_top) * k / n
            r = 0.0044 + 0.0022 * k / n
            rings += [(z - 0.002, r, (0, 0)), (z, r + 0.0004, (0, 0))] if 0 < k < n else [(z, r, (0, 0))]
        b.ring_tube(sorted(rings), 16, "CLUB chrome", cap_top=False, cap_bottom=False)
    b.ring_tube([(ferrule_top - 0.030, 0.0068, (0, 0)), (ferrule_top - 0.004, 0.0056, (0, 0)), (ferrule_top, 0.0048, (0, 0))], 16, "CLUB black")
    b.ring_tube([(ferrule_top - 0.030, 0.0070, (0, 0)), (ferrule_top - 0.027, 0.0070, (0, 0))], 16, "CLUB accent", cap_top=False, cap_bottom=False)
    return ferrule_top - 0.030


def driver(b):
    sole = SOLE["Driver"]; loft = math.radians(LOFT["Driver"])
    # top view: heel at x = -0.024, toe at 0.110; face along y = -0.030, back to 0.078
    N = 64
    outline = []
    for i in range(N):
        a = 2 * math.pi * i / N
        c, s = math.cos(a), math.sin(a)
        # a pear: squarer toward the face (-y), round and wide at the back
        rx = 0.067; ry = 0.056 if s > 0 else 0.052
        e = 2.2 if s < 0 else 2.0
        x = 0.043 + rx * math.copysign(abs(c) ** (2 / e), c)
        y = 0.022 + ry * math.copysign(abs(s) ** (2 / e), s)
        outline.append((x, y))
    H = 0.060
    def top_height(y):   # the crown falls from the face to the back
        u = min(max((y + 0.030) / 0.108, 0), 1)
        return H * (1 - 0.42 * u ** 1.4)
    K = 14
    rows, kinds = [], []
    for k in range(K + 1):
        t = k / K
        # the sole is flat with a rolled skirt; the crown domes over
        if t < 0.12: s_in = 0.93 + 0.07 * math.sin(t / 0.12 * math.pi / 2)
        elif t < 0.62: s_in = 1.0
        else: s_in = math.sqrt(max(0.0, 1 - ((t - 0.62) / 0.38) ** 2))
        row = []
        for x, y in outline:
            cx, cy = 0.043, 0.024
            px, py = cx + (x - cx) * s_in, cy + (y - cy) * s_in
            z = sole + top_height(y) * t
            # lay the face back by the loft, most at the face and none at the back
            front = max(0.0, min(1.0, (0.0 - y) / 0.030))
            py += (z - sole) * math.tan(loft) * front
            row.append((px, py, z))
        rows.append(row)
    def mat(i, j):
        x, y = outline[j]; t = (i + 0.5) / K
        if y < -0.022 and 0.14 < t < 0.78 and -0.012 < x < 0.100: return "CLUB gunmetal"
        if t < 0.26: return "CLUB satin"
        return "CLUB carbon"
    b.grid_solid(rows[:-1], mat, cap="fan")   # the crown's last ring closes to a point
    # scorelines on the face and a teal alignment chevron on the crown
    n = Vector((0, -math.cos(loft), math.sin(loft)))
    for k in range(5):
        z = sole + 0.016 + k * 0.007
        y = -0.0315 + (z - sole) * math.tan(loft)
        b.box(Vector((0.044, y - 0.0004, z)), (0.050, 0.0012, 0.0010), "CLUB groove", Matrix.Rotation(-loft, 3, 'X'))
    b.box(Vector((0.045, 0.004, sole + 0.0605)), (0.010, 0.004, 0.0015), "CLUB accent")
    # hosel from the heel of the crown up to the ferrule
    b.ring_tube([(sole + 0.030, 0.0078, (0, 0)), (sole + 0.064, 0.0072, (0, 0)), (FERRULE_TOP["Driver"] - 0.030, 0.0068, (0, 0))], 16, "CLUB satin")


def blade(b, club):
    """An iron or wedge: face outline in x-z, laid back by the loft, thick at the sole and thin at
    the topline, a cavity badge behind."""
    sole = SOLE[club]; loft = math.radians(LOFT[club]); wedge = club == "Wedge"
    toe_h = 0.056 if wedge else 0.050; heel_h = 0.030 if wedge else 0.028
    L = 0.088
    N = 40
    # a round-toed outline: low at the heel, rising to the toe
    pts = []
    for i in range(N):
        a = 2 * math.pi * i / N
        c, s = math.cos(a), math.sin(a)
        x = L / 2 + (L / 2) * math.copysign(abs(c) ** 0.55, c)
        hx = heel_h + (toe_h - heel_h) * (x / L) ** 1.3
        z = hx / 2 + (hx / 2) * math.copysign(abs(s) ** (0.45 if s < 0 else 0.7), s)
        pts.append((x - 0.006, z))
    thick_sole = 0.028 if wedge else 0.022
    rows = []
    for layer in range(4):
        row = []
        for x, z in pts:
            zz = sole + z
            yf = -0.014 + z * math.tan(loft)
            th = 0.006 + (thick_sole - 0.006) * (1 - min(z / 0.030, 1)) ** 1.5
            y = [yf, yf + 0.0015, yf + th - 0.0015, yf + th][layer]
            shrink = [0.0, 0.0015, 0.0015, 0.0][layer]
            cx, cz = L / 2 - 0.006, 0.026
            row.append((x + (cx - x) * shrink * 8, y, zz + (sole + cz - zz) * shrink * 8))
        rows.append(row)
    finish = "CLUB satin" if wedge else "CLUB chrome"
    b.grid_solid(rows, lambda i, j: finish)
    # grooves across the face
    n_g = 11 if wedge else 9
    for k in range(n_g):
        z = 0.006 + k * (0.0038 if wedge else 0.0040)
        b.box(Vector((0.036, -0.0142 + z * math.tan(loft) - 0.0002, sole + z)), (0.056, 0.0008, 0.0009), "CLUB groove", Matrix.Rotation(-loft, 3, 'X'))
    # cavity badge on the back
    zb = 0.020
    b.box(Vector((0.040, -0.014 + zb * math.tan(loft) + 0.006 + 0.0022, sole + zb)), (0.042, 0.002, 0.012), "CLUB accent", Matrix.Rotation(-loft, 3, 'X'))
    # hosel
    b.ring_tube([(sole + 0.008, 0.0062, (0, 0.002)), (sole + 0.040, 0.0064, (0, 0.0)), (FERRULE_TOP[club] - 0.030, 0.0068, (0, 0))], 16, finish)


def putter(b):
    sole = SOLE["Putter"]; loft = math.radians(LOFT["Putter"])
    # a heel-toe weighted blade: 0.12 long, 0.028 tall, 0.030 deep with a flange
    x0, x1 = -0.026, 0.094
    N = 32
    def slab(y0, y1, z0, z1, name, r=0.004):
        rows = []
        for z in (z0, z0 + r * 0.5, z1 - r * 0.5, z1):
            row = []
            for i in range(N):
                a = 2 * math.pi * i / N
                c, s = math.cos(a), math.sin(a)
                x = (x0 + x1) / 2 + (x1 - x0) / 2 * math.copysign(abs(c) ** 0.18, c)
                y = (y0 + y1) / 2 + (y1 - y0) / 2 * math.copysign(abs(s) ** 0.18, s)
                inset = 0.0 if z0 + r * 0.4 < z < z1 - r * 0.4 else 0.0012
                row.append((x - math.copysign(inset, c), y - math.copysign(inset, s) + (z - sole) * math.tan(loft) * (1 if s < 0 else 0), z))
            rows.append(row)
        b.grid_solid(rows, lambda i, j: name)
    slab(-0.024, 0.006, sole, sole + 0.027, "CLUB satin")          # the blade, face at -y
    slab(-0.004, 0.024, sole, sole + 0.012, "CLUB gunmetal")       # the flange behind
    b.box(Vector((0.034, -0.0242, sole + 0.014)), (0.070, 0.0008, 0.016), "CLUB black")   # face insert
    b.box(Vector((0.034, 0.010, sole + 0.0125)), (0.0018, 0.022, 0.0008), "CLUB white")   # sight line
    b.box(Vector((0.034, -0.010, sole + 0.0275)), (0.0018, 0.020, 0.0008), "CLUB white")
    # plumber's neck: up from the heel-centre of the top, a jog, up to the ferrule
    hx = 0.004
    b.ring_tube([(sole + 0.026, 0.0045, (hx, -0.010)), (sole + 0.040, 0.0045, (hx, -0.010))], 12, "CLUB satin")
    b.box(Vector((hx / 2, -0.005, sole + 0.042)), (abs(hx) + 0.009, 0.014, 0.006), "CLUB satin")
    b.ring_tube([(sole + 0.042, 0.0048, (0, 0)), (FERRULE_TOP["Putter"] - 0.030, 0.0068, (0, 0))], 12, "CLUB satin")


def build(club, name=None):
    b = Builder()
    shaft_and_grip(b, club)
    if club == "Driver": driver(b)
    elif club == "Putter": putter(b)
    else: blade(b, club)
    return b.finish(name or f"CLUB_{club.upper()}")


if __name__ == "__main__":
    # Preview: all four side by side in a fresh scene.
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for i, c in enumerate(CLUBS):
        ob = build(c)
        bpy.context.scene.collection.objects.link(ob)
        ob.location.x = i * 0.25
