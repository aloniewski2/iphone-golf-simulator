"""More head styles for the avatar kit: haircuts, hats, glasses and facial hair, in the same head space and with the same
promises as avatar_head_parts.py (hair stays under HAIR_TOP; a hat's inside is HAT_CLEAR off the skull; roles only).
Registered, with the originals, in avatar_catalog.py.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
import avatar_kit as K
import avatar_base as B
from avatar_kit import tube, blob, join, lathe, ring_tube, ellipse_plate, Surface
from avatar_base import HX, HY, HZ
from avatar_head_parts import shell, _quad, _c, _cap, ribbon, _bill, _flat_ring, HAT_CLEAR, HAIR_TOP, mustache


def _place(o, center, rot_y=0.0, rot_x=0.0, rot_z=0.0):
    if rot_x: o.data.transform(Matrix.Rotation(rot_x, 4, 'X'))
    if rot_y: o.data.transform(Matrix.Rotation(rot_y, 4, 'Y'))
    if rot_z: o.data.transform(Matrix.Rotation(rot_z, 4, 'Z'))
    o.data.transform(Matrix.Translation(Vector(center)))
    return o


# ------------------------------------------------------------------------------------------------ haircuts
def afro(head, under_hat=False):
    """A big round puff of hair, bumpy at the edge, standing clear of the ears."""
    cap = _cap(front=0.50, side=0.22, back=-0.20, thick=0.014, crown=0.004)
    objs = [cap]
    if not under_hat:
        objs.append(blob("Puff", (0, 0.055, 0.175), (0.290, 0.262, 0.235), "Hero_Hair", segs=18, rings=12, p=2.15))
        r = random.Random(5)
        for k in range(14):                                      # clumps round the rim: the edge is not one smooth ball
            a = 2 * math.pi * k / 14 + 0.2
            x, y = 0.285 * math.cos(a) * 0.92, 0.055 + 0.262 * math.sin(a) * 0.92
            if y < -0.150: continue                              # never over the forehead
            objs.append(blob("Clump", (x, y, 0.175 + 0.235 * (0.30 * math.sin(2 * a)) + r.uniform(-0.02, 0.03)), (0.066, 0.066, 0.060), "Hero_HairB" if k % 4 == 0 else "Hero_Hair", segs=8, rings=6))
    return join("Hair_Afro", objs)


def bun(head, under_hat=False):
    """A tidy cap and a top-knot (low at the back under a hat), tied."""
    cap = _cap(front=0.50, side=0.24, back=-0.22, thick=0.020, crown=0.006 if not under_hat else 0.002)
    surf = Surface(head); objs = [cap]
    if not under_hat:
        objs += [ribbon(surf, [(-60, 70, 0.004), (-30, 56, 0.016), (4, 46, 0.020), (36, 38, 0.014), (62, 30, 0.006)], 0.050, 0.042, 0.020, "Hero_Hair", "SwoopA", base=0.014),
                 ribbon(surf, [(-64, 52, 0.002), (-32, 42, 0.010), (2, 36, 0.012), (34, 30, 0.008)], 0.042, 0.034, 0.016, "Hero_HairB", "SwoopB", base=0.014)]
        objs.append(blob("Knot", (0, 0.045, 0.258), (0.086, 0.086, 0.078), "Hero_Hair", segs=12, rings=8))
        objs.append(blob("KnotB", (0.022, 0.050, 0.282), (0.050, 0.050, 0.042), "Hero_HairB", segs=8, rings=6))
        tie = _flat_ring("Tie", 0.0, 0.074, 0.010, "Hero_HatB", segs=14); tie.data.transform(Matrix.Translation((0, 0.045, 0.226))); objs.append(tie)
    else:
        objs.append(blob("Knot", (0, 0.200, 0.095), (0.070, 0.062, 0.066), "Hero_Hair", segs=10, rings=7))
    return join("Hair_Bun", [o for o in objs if o])


def pigtails(head, under_hat=False):
    """A cap, a fringe, and a high tail on each side, tied with a bobble."""
    cap = _cap(front=0.50, side=0.22, back=-0.20, thick=0.020, crown=0.006 if not under_hat else 0.002)
    surf = Surface(head); objs = [cap]
    if not under_hat:
        objs += [ribbon(surf, [(-60, 66, 0.004), (-28, 52, 0.016), (6, 44, 0.020), (38, 36, 0.014), (62, 28, 0.006)], 0.050, 0.042, 0.020, "Hero_Hair", "FringeA", base=0.014)]
    for s in (1, -1):
        objs.append(tube("Tail", [(s * 0.185, 0.050, 0.130), (s * 0.255, 0.070, 0.095), (s * 0.292, 0.090, 0.000), (s * 0.282, 0.090, -0.120), (s * 0.262, 0.080, -0.215)],
                         [(0.040, 0.040), (0.050, 0.050), (0.054, 0.052), (0.044, 0.042), (0.012, 0.012)], "Hero_Hair", segs=10, per=2))
        objs.append(blob("Bobble", (s * 0.215, 0.060, 0.118), (0.040, 0.040, 0.040), "Hero_HatB", segs=8, rings=6))
    return join("Hair_Pigtails", objs)


def mohawk(head, under_hat=False):
    """The sides shaved bare; a fin of hair from the brow to the nape."""
    surf = Surface(head); pts = []
    spec = [(0, 34, 0.020), (0, 56, 0.026), (0, 78, 0.028), (0, 89.9, 0.028), (180, 76, 0.028), (180, 52, 0.022), (180, 30, 0.016)]
    for az, el, lift in spec:
        a, e = math.radians(az), math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))).normalized()
        p, n = surf.ray(d * 0.6, -d, 0.8)
        if p is not None: pts.append(p + n * lift)
    h = 0.040 if under_hat else 0.082
    radii = [(h * 0.45, 0.030), (h * 0.8, 0.032), (h, 0.034), (h, 0.034), (h * 0.85, 0.032), (h * 0.6, 0.030), (h * 0.35, 0.026)]
    fin = tube("Fin", pts, radii[:len(pts)], "Hero_Hair", segs=8, per=3, ref=Vector((1, 0, 0)), cap_rings=2)
    return join("Hair_Mohawk", [fin])


def quiff(head, under_hat=False):
    """Short sides and a tall swept-up wave over the forehead."""
    cap = _cap(front=0.44, side=0.18, back=-0.14, thick=0.016, crown=0.006)
    surf = Surface(head); objs = [cap]
    if not under_hat:
        objs += [ribbon(surf, [(0, 80, 0.030), (0, 66, 0.062), (0, 52, 0.084), (0, 40, 0.074), (0, 31, 0.044)], 0.082, 0.070, 0.036, "Hero_Hair", "Wave", base=0.014),
                 ribbon(surf, [(-46, 72, 0.020), (-24, 58, 0.050), (-4, 46, 0.066), (8, 36, 0.050)], 0.052, 0.044, 0.026, "Hero_HairB", "WaveL", base=0.014),
                 ribbon(surf, [(46, 72, 0.020), (24, 58, 0.050), (4, 46, 0.066), (-8, 36, 0.050)], 0.052, 0.044, 0.026, "Hero_HairB", "WaveR", base=0.014)]
    return join("Hair_Quiff", [o for o in objs if o])


def buzz(head, under_hat=False):
    """Clippered close: a thin cap on the skull."""
    return join("Hair_Buzz", [_cap(front=0.42, side=0.16, back=-0.12, thick=0.008, crown=0.002)])


def braids(head, under_hat=False):
    """A cap, and a long braid down each side, each a chain of knots."""
    cap = _cap(front=0.50, side=0.20, back=-0.25, thick=0.020, crown=0.006 if not under_hat else 0.002)
    surf = Surface(head); objs = [cap]
    if not under_hat:
        objs += [ribbon(surf, [(-58, 68, 0.004), (-28, 54, 0.016), (6, 44, 0.020), (38, 36, 0.014), (62, 28, 0.006)], 0.050, 0.042, 0.020, "Hero_Hair", "FringeA", base=0.014)]
    for s in (1, -1):
        ctrl = [(s * 0.165, 0.110, 0.090), (s * 0.190, 0.090, -0.020), (s * 0.205, 0.050, -0.140), (s * 0.205, 0.010, -0.250), (s * 0.200, -0.030, -0.350)]
        pts = K.catmull([Vector(c) for c in ctrl], 3)
        step = max(1, len(pts) // 9)
        for i, c in enumerate(pts[::step]):
            rad = 0.050 - 0.0030 * i
            objs.append(blob("Plait", c, (rad, rad * 0.95, rad * 1.18), "Hero_HairB" if i % 2 else "Hero_Hair", segs=8, rings=6, rot=(0, 0, 0.5 * (-1) ** i)))
        objs.append(blob("Bobble", (s * 0.198, -0.040, -0.372), (0.028, 0.028, 0.028), "Hero_HatB", segs=6, rings=5))
    return join("Hair_Braids", objs)


# ------------------------------------------------------------------------------------------------ hats
def headband(head):
    """A cloth band round the brow, hair above and below it."""
    lo, hi = _quad(0.28, 0.10, 0.0), _quad(0.58, 0.46, 0.34)
    band = shell("Band", lambda p: lo(_c(p)) < p.z / HZ < hi(_c(p)), lambda p: 0.026, "Hero_HatA", rim=True)
    return join("Hat_Headband", [band])


def beret(head):
    """A soft flat beret, tipped to one side, with a stalk."""
    prof = [(0, 0.218), (0.10, 0.214), (0.20, 0.198), (0.262, 0.162), (0.288, 0.118), (0.282, 0.086), (0.238, 0.070), (0.214, 0.072)]
    o = lathe("Beret", prof, "Hero_HatA", segs=24, sx=1.0, sy=0.96)
    stalk = blob("Stalk", (0, 0, 0.226), (0.014, 0.014, 0.020), "Hero_HatB", segs=8, rings=5)
    h = join("Hat_Beret", [o, stalk])
    _place(h, (0.030, -0.005, 0.0), rot_y=-0.20)
    return h


def flatcap(head):
    """A flat newsboy cap with a short bill."""
    prof = [(0, 0.186), (0.12, 0.183), (0.22, 0.170), (0.274, 0.138), (0.290, 0.100), (0.272, 0.076), (0.224, 0.070), (0.208, 0.076)]
    o = lathe("Flat", prof, "Hero_HatA", segs=24, sx=1.04, sy=1.10, center=(0, -0.012, 0))
    return join("Hat_Flatcap", [o, _bill(reach=0.32, width=0.098, z=0.084, drop=0.018), blob("Button", (0, -0.012, 0.188), (0.014, 0.014, 0.010), "Hero_HatB", segs=8, rings=5)])


def fedora(head):
    """A fedora: a creased crown, a band, a brim turned down at the front."""
    crown = [(0, 0.262), (0.06, 0.270), (0.115, 0.262), (0.185, 0.272), (0.226, 0.250), (0.240, 0.200), (0.242, 0.110), (0.244, 0.082)]
    brim = [(0.244, 0.082), (0.300, 0.074), (0.352, 0.082), (0.372, 0.096), (0.370, 0.086), (0.348, 0.070), (0.298, 0.060), (0.246, 0.064)]
    o = join("Hat_Fedora", [lathe("Crown", crown, "Hero_HatA", segs=24, sx=1.0, sy=1.06), lathe("Brim", brim, "Hero_HatA", segs=24, sx=1.0, sy=1.06),
                            _flat_ring("Band", 0.118, 0.244, 0.013, "Hero_HatB")])
    return o


def cowboy(head):
    """A cowboy hat: a dented crown and a wide brim curled up at the sides."""
    crown = [(0, 0.246), (0.05, 0.236), (0.11, 0.264), (0.19, 0.270), (0.232, 0.244), (0.242, 0.190), (0.244, 0.100), (0.246, 0.080)]
    brim = [(0.246, 0.080), (0.31, 0.066), (0.39, 0.056), (0.46, 0.082), (0.50, 0.136), (0.494, 0.120), (0.456, 0.070), (0.388, 0.040), (0.31, 0.052), (0.248, 0.062)]
    return join("Hat_Cowboy", [lathe("Crown", crown, "Hero_HatA", segs=26, sx=1.0, sy=1.06), lathe("Brim", brim, "Hero_HatA", segs=26, sx=1.0, sy=1.04),
                               _flat_ring("Band", 0.112, 0.246, 0.012, "Hero_HatB")])


def headphones(head):
    """Over-ear headphones: a band over the crown and a cushioned cup on each ear."""
    arch = [(0.236 * math.sin(math.radians(a)), 0.008, 0.012 + 0.236 * math.cos(math.radians(a))) for a in range(-90, 91, 30)]
    objs = [tube("Band", arch, 0.0125, "Hero_HatA", segs=8, per=3, cap_rings=2)]
    for s in (1, -1):
        objs.append(blob("Cup", (s * 0.246, 0.006, -0.004), (0.036, 0.074, 0.080), "Hero_HatA", segs=12, rings=8))
        objs.append(blob("Cushion", (s * 0.218, 0.006, -0.004), (0.017, 0.064, 0.070), "Hero_HatB", segs=10, rings=7))
    return join("Hat_Headphones", objs)


def bandana(head):
    """A bandana knotted at the back, its two tails hanging."""
    dome = shell("Dome", lambda p: p.z / HZ > _quad(0.28, 0.08, -0.06)(_c(p)), lambda p: HAT_CLEAR, "Hero_HatA")
    objs = [dome, blob("Knot", (0, 0.215, 0.060), (0.034, 0.030, 0.034), "Hero_HatA", segs=8, rings=6)]
    for s in (1, -1):
        objs.append(tube("Tail", [(s * 0.012, 0.220, 0.050), (s * 0.040, 0.250, -0.020), (s * 0.058, 0.262, -0.110)], [(0.026, 0.012), (0.034, 0.012), (0.040, 0.010)], "Hero_HatB", segs=8, per=2, cap_rings=2))
    return join("Hat_Bandana", objs)


def tophat(head):
    """A tall silk top hat with a band."""
    crown = [(0, 0.440), (0.16, 0.440), (0.205, 0.430), (0.216, 0.400), (0.216, 0.100), (0.216, 0.084)]
    brim = [(0.216, 0.084), (0.30, 0.076), (0.335, 0.090), (0.30, 0.106), (0.216, 0.104)]
    return join("Hat_Tophat", [lathe("Crown", crown, "Hero_HatA", segs=24, sx=1.0, sy=1.06), lathe("Brim", brim, "Hero_HatA", segs=24, sx=1.0, sy=1.06),
                               _flat_ring("Band", 0.128, 0.219, 0.016, "Hero_HatB")])


def crown(head):
    """A gold crown: a band round the head and five points, a jewel on each."""
    objs = [lathe("Band", [(0.206, 0.100), (0.222, 0.100), (0.232, 0.160), (0.214, 0.160)], "Hero_HatA", segs=20, sx=1.0, sy=1.06)]
    for k in range(5):
        a = 2 * math.pi * k / 5 + math.pi / 2
        x, y = 0.226 * math.cos(a), 0.226 * 1.06 * math.sin(a)
        objs.append(tube("Point", [(x, y, 0.150), (x * 1.02, y * 1.02, 0.218), (x * 1.03, y * 1.03, 0.282)], [(0.032, 0.020), (0.022, 0.016), (0.004, 0.004)], "Hero_HatA", segs=8, per=1, cap_rings=1, ref=Vector((0, 0, 1))))
        objs.append(blob("Jewel", (x * 1.03, y * 1.03, 0.288), (0.016, 0.016, 0.016), "Hero_HatB", segs=6, rings=5))
    objs.append(blob("Gem", (0, -0.236, 0.130), (0.020, 0.010, 0.020), "Hero_HatB", segs=8, rings=5))
    return join("Hat_Crown", objs)


# ------------------------------------------------------------------------------------------------ glasses
def _specs(head, lenses, frame_r, lens_mat, name, frame_mat="Hero_Frame", dz=-0.008):
    """lenses: [(centre x, rx, rz, exponent, tilt)]; one pair, or one wide lens at x = 0. Temples hug the skull to the ears."""
    surf = Surface(head)
    p0, _ = surf.hit(0.08, dz); yf = p0.y - 0.020
    parts = []
    for cx, rx, rz, p, tilt in lenses:
        side = 1 if cx > 0 else -1
        ring = ring_tube("Rim", (0, 0, 0), rx, rz, frame_r, frame_mat, segs=24, sides=6, p=p)
        plate = ellipse_plate("Lens", (0, 0, 0), rx - frame_r * 0.4, rz - frame_r * 0.4, lens_mat, segs=20, thick=0.002, p=p)
        for o in (ring, plate): _place(o, (cx, yf, dz), rot_y=side * tilt)
        parts += [ring, plate]
    outer = max(abs(cx) + rx for cx, rx, *_ in lenses)
    for s in (1, -1):
        pts = [Vector((s * outer, yf, dz + 0.012))]
        for yy in (-0.05, 0.0, 0.05):
            q, _ = surf.ray((s * 1.0, yy, dz + 0.006), (-s, 0, 0), 2.0)
            if q is not None: pts.append(Vector((q.x + s * 0.012, yy, dz + 0.006)))
        parts.append(tube("Arm", pts, frame_r * 0.95, frame_mat, segs=6, per=3, cap_rings=2))
    if len(lenses) == 2:
        cx, rx, rz = lenses[0][0], lenses[0][1], lenses[0][2]
        parts.append(tube("Bridge", [(-(cx - rx), yf - 0.004, dz + rz * 0.40), (0, yf - 0.012, dz + rz * 0.55), ((cx - rx), yf - 0.004, dz + rz * 0.40)], frame_r * 0.9, frame_mat, segs=6, per=3, cap_rings=2))
    return join(name, parts)


def aviators(head):
    return _specs(head, [(0.080, 0.056, 0.050, 2.15, -0.12), (-0.080, 0.056, 0.050, 2.15, -0.12)], 0.0045, "Hero_LensDark", "Glasses_Aviator", frame_mat="Hero_Metal")


def cateye(head):
    return _specs(head, [(0.080, 0.054, 0.041, 2.3, 0.26), (-0.080, 0.054, 0.041, 2.3, 0.26)], 0.0062, "Hero_Lens", "Glasses_Cateye")


def wrap_shades(head):
    return _specs(head, [(0.0, 0.130, 0.040, 3.0, 0.0)], 0.0085, "Hero_LensDark", "Glasses_Wrap")


# ------------------------------------------------------------------------------------------------ facial hair
def goatee(head):
    """A chin patch and a thin moustache, mouth clear."""
    patch = shell("Goatee", lambda p: p.z / HZ < -0.62 and abs(p.x) < 0.060 and p.y < 0.02, lambda p: 0.014, "Hero_Hair", passes=4)
    parts = [patch]
    m = mustache(head)
    if m: parts.append(m)
    return join("Face_Goatee", parts)


def handlebar(head):
    """A waxed moustache, its tips curled up."""
    surf = Surface(head); parts = []
    for side in (1, -1):
        pts = []
        for x, z, lift in ((0.004, -0.080, 0.008), (0.032, -0.088, 0.008), (0.062, -0.084, 0.008), (0.088, -0.070, 0.016), (0.104, -0.048, 0.026)):
            p, n = surf.hit(side * x, z)
            if p is None: return None
            pts.append(p + n * lift)
        parts.append(tube("Stache", pts, [(0.014, 0.012), (0.0135, 0.012), (0.011, 0.010), (0.0085, 0.008), (0.005, 0.005)], "Hero_Hair", segs=8, per=3, cap_rings=2))
    return join("Face_Handlebar", parts)


def stubble(head):
    """Five o'clock shadow along the jaw and chin."""
    def keep(p):
        z = p.z / HZ
        if z > -0.14 or z < -0.92 or p.y > 0.09: return False
        return not (abs(p.x) < 0.075 and -0.60 < z < -0.30)      # the mouth is clear
    return join("Face_Stubble", [shell("Stubble", keep, lambda p: 0.004, "Hero_HairB", passes=4)])
