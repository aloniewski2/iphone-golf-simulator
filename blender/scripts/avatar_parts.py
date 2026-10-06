"""The parts library: every swappable piece of the Hero, made against the numbers in avatar_base.py.

A part is a function that returns one mesh (or a list) and is registered under a slot. The slots and what they
promise are in PARTS at the bottom; the contract (what a hat may assume about a haircut, what a top may assume
about the body) is in docs/avatar-kit.md.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
import avatar_kit as K
import avatar_base as B
from avatar_kit import tube, blob, join, Surface
from avatar_base import C, HX, HY, HZ, J, SPINE_Y, mir


# ------------------------------------------------------------------------------------------------ tops
def polo():
    """The golf polo: a boxy torso, puffy short sleeves with a trim band, a two-wing collar, a placket with two
    buttons. Roles: Top (the cloth), TopTrim (collar, sleeve bands), Accent (placket), Lace (buttons)."""
    parts = []
    z = [0.980, 1.03, 1.10, 1.17, 1.22, 1.252, 1.272]
    rx = [0.192, 0.200, 0.206, 0.206, 0.186, 0.134, 0.090]
    ry = [0.139, 0.142, 0.140, 0.134, 0.122, 0.102, 0.084]
    parts.append(tube("Torso", [(0, SPINE_Y + 0.004, zz) for zz in z], list(zip(rx, ry)), "Hero_Top", segs=24, p=2.5, per=2, cap0=False, cap1=False))
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        pts = [f((0.150, 0.055, 1.128)), f((0.218, 0.054, 1.104)), f((0.290, 0.052, 1.064)), f((0.345, 0.0505, 1.000))]
        parts.append(tube("Sleeve", pts, [(0.108, 0.104), (0.107, 0.103), (0.104, 0.100), (0.098, 0.094)], "Hero_Top", segs=16, per=2, cap0=False, cap1=False))
        parts.append(blob("Shoulder", f((0.175, 0.055, 1.122)), (0.108, 0.104, 0.100), "Hero_Top", segs=14, rings=9))
        t = (Vector(pts[-1]) - Vector(pts[-2])).normalized(); e = Vector(pts[-1]); e = Vector((e.x * side, e.y, e.z))
        t = Vector((t.x * side, t.y, t.z))
        a = e - t * 0.005; b = e + t * 0.030; c = e + t * 0.044
        parts.append(tube("CuffBand", [f(a), f(b)], [(0.1005, 0.0965)] * 2, "Hero_TopTrim", segs=16, per=1, cap0=False, cap1=False))
        parts.append(tube("CuffStripe", [f(b), f(c)], [(0.1015, 0.0975)] * 2, "Hero_Accent", segs=16, per=1, cap0=False, cap1=False))
    parts.append(tube("CollarBand", [(0, SPINE_Y + 0.004, 1.236), (0, SPINE_Y + 0.004, 1.284)], [(0.108, 0.092), (0.086, 0.074)], "Hero_TopTrim", segs=24, p=2.2, per=2, cap0=False, cap1=False))
    for side in (1, -1):
        parts.append(blob("CollarWing", (side * 0.058, SPINE_Y - 0.084, 1.236), (0.056, 0.012, 0.046), "Hero_TopTrim", rot=(0.35, side * -0.55, side * 0.25), segs=10, rings=6))
    parts.append(tube("Placket", [(0, SPINE_Y - 0.100, 1.222), (0, SPINE_Y - 0.128, 1.10)], [(0.0075, 0.0055)] * 2, "Hero_Accent", segs=8, per=2))
    for zz in (1.190, 1.140):
        parts.append(blob("Button", (0, SPINE_Y - 0.124 - (1.222 - zz) * 0.22, zz), (0.0105, 0.0055, 0.0105), "Hero_Lace", segs=8, rings=5))
    return join("Top_Polo", parts)


# ------------------------------------------------------------------------------------------------ bottoms
def shorts():
    """Shorts: a pelvis that widens into the hips and two legs that start inside it, a drawstring with a knot and an
    Accent pipe down each outer seam. Roles: Bottom, Accent, Lace."""
    # the pelvis and the two legs are closed shells that overlap; fusing them (a voxel remesh, cut at the waist and at
    # the hem, then decimated) gives one smooth surface with a proper crotch and no ledge where they meet
    raw = [tube("Pelvis", [(0, SPINE_Y + 0.004, z) for z in (0.995, 0.93, 0.86, 0.80, 0.77)],
                [(0.190, 0.137), (0.200, 0.138), (0.214, 0.134), (0.221, 0.128), (0.222, 0.122)], "Hero_Bottom", segs=24, p=2.4, per=2, flat1=True)]
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        # the legs leave the pelvis and part below the crotch (their inner edges clear the middle from z 0.76 down)
        raw.append(tube("ShortLeg", [f((0.100, 0.055, 0.90)), f((0.110, 0.055, 0.80)), f((0.132, 0.055, 0.70)), f((0.146, 0.055, 0.62)), f((0.156, 0.055, 0.56)), f((0.160, 0.055, 0.50))],
                        [(0.112, 0.118), (0.113, 0.120), (0.115, 0.122), (0.118, 0.126), (0.120, 0.130), (0.122, 0.132)], "Hero_Bottom", segs=16, p=2.2, per=2))
    parts = [_fuse("ShortsShell", raw, voxel=0.010, z_lo=0.56, z_hi=0.995, tris=1500)]
    parts.append(tube("Belt", [(0, SPINE_Y + 0.004, 0.962), (0, SPINE_Y + 0.004, 0.992)], [(0.1965, 0.1425)] * 2, "Hero_Accent", segs=24, p=2.4, per=1, cap0=False, cap1=False))
    parts.append(blob("Buckle", (0, SPINE_Y - 0.1435, 0.977), (0.020, 0.0065, 0.017), "Hero_Metal", segs=10, rings=6, p=3.0))
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        parts.append(_flat_band(f((0.156, 0.055, 0.5625)), 0.120, 0.130, 0.0075, "Hero_Bottom", segs=16))                          # the hem's thickness
        parts.append(tube("Pipe", [f((0.190, 0.055, 0.940)), f((0.212, 0.055, 0.885)), f((0.223, 0.055, 0.80)), f((0.246, 0.055, 0.70)), f((0.276, 0.055, 0.56))],
                           [(0.0045, 0.0045)] * 5, "Hero_Accent", segs=6, per=2))
    return join("Bottom_Shorts", parts)


def _fuse(name, parts, voxel, z_lo, z_hi, tris):
    """Overlapping closed shells as one smooth surface: voxel-remeshed, cut open at z_lo and z_hi (the hem and the
    waist), and decimated to about `tris` triangles."""
    o = join(name, parts)
    m = o.modifiers.new("Remesh", 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel; m.use_smooth_shade = True
    K.apply_modifiers(o)
    bm = bmesh.new(); bm.from_mesh(o.data)
    for z, outer in ((z_lo, False), (z_hi, True)):
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, z), plane_no=(0, 0, 1), clear_inner=not outer, clear_outer=outer)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    n = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if n > tris:
        d = o.modifiers.new("Decimate", 'DECIMATE'); d.ratio = tris / n
        K.apply_modifiers(o)
    for p in o.data.polygons: p.use_smooth = True
    return o


# ------------------------------------------------------------------------------------------------ shoes
def _flat_band(center, rx, ry, r, mat, segs=14, sides=5):
    """A thin round-section ring (a rolled cuff) lying flat about a vertical axis at `center`."""
    o = K.ring_tube("Roll", (0, 0, 0), rx, rx, r, mat, segs=segs, sides=sides)
    o.data.transform(Matrix.Rotation(math.radians(90), 4, 'X'))
    o.data.transform(Matrix.Diagonal((1.0, ry / rx, 1.0, 1.0)))
    o.data.transform(Matrix.Translation(Vector(center)))
    return o


def golf_shoes():
    """Socks and chunky golf sneakers: a rounded upper, a thick sole, a heel tab, laces and a side stripe.
    Roles: Sock, Shoe, ShoeSole, Lace, Accent. (Along a path that runs front to back a tube's first radius is its
    height and its second its width.)"""
    parts = []
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        parts.append(tube("Sock", [f((0.172, 0.055, 0.30)), f((0.180, 0.055, 0.225)), f((0.187, 0.055, 0.15))], [(0.0700, 0.0690), (0.0685, 0.0675), (0.0675, 0.0665)], "Hero_Sock", segs=14, per=2, cap0=False, cap1=False))
        parts.append(_flat_band(f((0.1716, 0.055, 0.300)), 0.0700, 0.0690, 0.0085, "Hero_Sock"))                 # the rolled top
        parts.append(tube("SockBand", [f((0.1735, 0.055, 0.280)), f((0.1745, 0.055, 0.264))], [(0.0712, 0.0702)] * 2, "Hero_TopTrim", segs=14, per=1, cap0=False, cap1=False))
        # the upper stands on the sole (its underside at z ~ 0.03): a tall heel, a low round toe box
        pts = [f((0.187, 0.050, 0.105)), f((0.187, 0.000, 0.098)), f((0.187, -0.060, 0.076)), f((0.187, -0.120, 0.062))]
        parts.append(tube("Upper", pts, [(0.075, 0.062), (0.072, 0.074), (0.050, 0.082), (0.038, 0.070)], "Hero_Shoe", segs=14, p=2.4, per=2, ref=Vector((1, 0, 0))))
        # the sole: a slim band standing a little proud of the upper all round
        parts.append(tube("Sole", [f((0.187, 0.058, 0.021)), f((0.187, -0.040, 0.021)), f((0.187, -0.118, 0.021))], [(0.022, 0.072), (0.022, 0.088), (0.022, 0.078)], "Hero_ShoeSole", segs=14, p=3.0, per=2, ref=Vector((1, 0, 0))))
        parts.append(tube("Stripe", [f((0.265, -0.000, 0.088)), f((0.268, -0.062, 0.070))], [(0.016, 0.0045)] * 2, "Hero_Accent", segs=8, per=1, ref=Vector((1, 0, 0))))
        for y, zt in ((0.020, 0.164), (-0.016, 0.147), (-0.052, 0.128), (-0.088, 0.113)):        # a lace over the tongue at each eyelet
            parts.append(tube("Lace", [f((0.150, y, zt - 0.030)), f((0.187, y - 0.002, zt + 0.004)), f((0.224, y, zt - 0.030))], [(0.0055, 0.0055)] * 3, "Hero_Lace", segs=6, per=1))
    return join("Shoes_Golf", parts)
