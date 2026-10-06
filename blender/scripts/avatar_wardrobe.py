"""More clothes for the avatar kit, on the same body as the polo and the shorts (avatar_parts.py): tops that cover the torso
a few millimetres clear of the body skin, bottoms fused to one smooth surface. Colour roles only: Top, TopTrim, Accent,
Lace (tops); Bottom, Accent, Metal (bottoms). Registered in avatar_catalog.py.
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix
import avatar_kit as K
import avatar_base as B
from avatar_kit import tube, blob, join, lathe, ring_tube
from avatar_base import SPINE_Y, mir
from avatar_parts import _fuse, _flat_band

SY = SPINE_Y + 0.004


def _torso(z, rx, ry, mat="Hero_Top"):
    return tube("Torso", [(0, SY, zz) for zz in z], list(zip(rx, ry)), mat, segs=24, p=2.5, per=2, cap0=False, cap1=False)


def _sleeve(side, pts, radii, mat="Hero_Top", shoulder=True):
    f = (lambda v: Vector(v)) if side == 1 else mir
    out = [tube("Sleeve", [f(p) for p in pts], radii, mat, segs=16, per=2, cap0=False, cap1=False)]
    if shoulder: out.append(blob("Shoulder", f((0.175, 0.055, 1.122)), (radii[0][0], radii[0][1], 0.100), mat, segs=14, rings=9))
    return out


def _cuff(side, pts, radius, length, mat):
    f = (lambda v: Vector(v)) if side == 1 else mir
    e = Vector(pts[-1]); t = (e - Vector(pts[-2])).normalized()
    a, b = e - t * 0.004, e + t * length
    return tube("Cuff", [f(tuple(a)), f(tuple(b))], [radius, radius], mat, segs=16, per=1, cap0=False, cap1=False)


def _neck(rx, ry, z0, z1, mat="Hero_TopTrim"):
    return tube("Neck", [(0, SY, z0), (0, SY, z1)], [(rx, ry), (rx * 0.82, ry * 0.84)], mat, segs=24, p=2.2, per=2, cap0=False, cap1=False)


# ------------------------------------------------------------------------------------------------ tops
def tee():
    """A crew-neck tee: short sleeves, a trim neck band, a badge on the chest."""
    parts = [_torso([0.975, 1.03, 1.10, 1.17, 1.22, 1.252, 1.272], [0.190, 0.198, 0.204, 0.204, 0.185, 0.134, 0.090], [0.138, 0.141, 0.139, 0.133, 0.121, 0.102, 0.084]),
             _neck(0.104, 0.088, 1.246, 1.286)]
    sl = [(0.150, 0.055, 1.128), (0.218, 0.054, 1.104), (0.290, 0.052, 1.064), (0.345, 0.0505, 1.000)]
    rd = [(0.108, 0.104), (0.107, 0.103), (0.104, 0.100), (0.098, 0.094)]
    for side in (1, -1):
        parts += _sleeve(side, sl, rd)
        parts.append(_cuff(side, sl, (0.100, 0.096), 0.018, "Hero_Accent"))
    parts.append(blob("Badge", (0.075, SY - 0.1385, 1.13), (0.030, 0.006, 0.030), "Hero_Accent", segs=10, rings=6))
    return join("Top_Tee", parts)


def hoodie():
    """A hoodie: long sleeves with cuffs, a hood bunched at the back of the neck, a pocket, drawcords, a hem band."""
    parts = [_torso([0.940, 1.00, 1.08, 1.16, 1.22, 1.252, 1.272], [0.205, 0.212, 0.220, 0.220, 0.200, 0.140, 0.098], [0.152, 0.155, 0.152, 0.146, 0.132, 0.108, 0.090]),
             tube("Hem", [(0, SY, 0.936), (0, SY, 0.972)], [(0.2075, 0.1545)] * 2, "Hero_TopTrim", segs=24, p=2.5, per=1, cap0=False, cap1=False),
             tube("Hood", [(-0.108, 0.070, 1.252), (-0.070, 0.134, 1.264), (0, 0.152, 1.268), (0.070, 0.134, 1.264), (0.108, 0.070, 1.252)], [(0.050, 0.050)] * 5, "Hero_Top", segs=10, per=2, cap_rings=2),
             blob("Pocket", (0, SY - 0.154, 1.02), (0.115, 0.012, 0.052), "Hero_TopTrim", segs=12, rings=6, p=2.6)]
    sl = [(0.150, 0.055, 1.128), (0.225, 0.054, 1.090), (0.300, 0.052, 1.030), (0.360, 0.050, 0.965), (0.420, 0.046, 0.870), (0.452, 0.043, 0.812)]
    rd = [(0.114, 0.110), (0.110, 0.106), (0.102, 0.098), (0.096, 0.092), (0.086, 0.082), (0.078, 0.074)]
    for side in (1, -1):
        parts += _sleeve(side, sl, rd)
        parts.append(_cuff(side, sl, (0.080, 0.076), 0.026, "Hero_TopTrim"))
    for s in (1, -1):
        parts.append(tube("Cord", [(s * 0.030, SY - 0.098, 1.250), (s * 0.034, SY - 0.130, 1.180), (s * 0.036, SY - 0.140, 1.115)], [(0.0045, 0.0045)] * 3, "Hero_Lace", segs=6, per=2, cap_rings=2))
    return join("Top_Hoodie", parts)


def vest():
    """A sleeveless sweater vest: bare shoulders, a trim neck, hem and armhole bands."""
    parts = [_torso([0.975, 1.03, 1.10, 1.17, 1.22, 1.246, 1.272], [0.190, 0.198, 0.202, 0.196, 0.168, 0.128, 0.092], [0.138, 0.141, 0.139, 0.132, 0.118, 0.100, 0.084]),
             _neck(0.100, 0.086, 1.244, 1.284),
             tube("Hem", [(0, SY, 0.972), (0, SY, 1.002)], [(0.1925, 0.1405)] * 2, "Hero_TopTrim", segs=24, p=2.5, per=1, cap0=False, cap1=False)]
    for s in (1, -1):                                              # a trim band round each armhole
        ring = ring_tube("Armhole", (0, 0, 0), 0.090, 0.098, 0.008, "Hero_TopTrim", segs=16, sides=5)
        ring.data.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))
        ring.data.transform(Matrix.Translation((s * 0.178, 0.055, 1.150)))
        parts.append(ring)
    parts.append(blob("Diamond", (0, SY - 0.1385, 1.10), (0.030, 0.006, 0.040), "Hero_Accent", segs=4, rings=4))
    return join("Top_Vest", parts)


# ------------------------------------------------------------------------------------------------ bottoms
def _belt(parts):
    parts.append(tube("Belt", [(0, SY, 0.962), (0, SY, 0.992)], [(0.1965, 0.1425)] * 2, "Hero_Accent", segs=24, p=2.4, per=1, cap0=False, cap1=False))
    parts.append(blob("Buckle", (0, SY - 0.1395, 0.977), (0.020, 0.0065, 0.017), "Hero_Metal", segs=10, rings=6, p=3.0))


def trousers():
    """Long trousers to the ankle, fused to one surface with a crotch, a belt and a turned-up hem."""
    raw = [tube("Pelvis", [(0, SY, z) for z in (0.995, 0.93, 0.86, 0.80, 0.77)],
                [(0.190, 0.137), (0.200, 0.138), (0.214, 0.134), (0.221, 0.128), (0.222, 0.122)], "Hero_Bottom", segs=24, p=2.4, per=2, flat1=True)]
    path = [(0.100, 0.90), (0.110, 0.80), (0.133, 0.61), (0.154, 0.44), (0.172, 0.30), (0.186, 0.20), (0.190, 0.14)]
    rad = [(0.112, 0.118), (0.113, 0.120), (0.114, 0.120), (0.104, 0.108), (0.095, 0.099), (0.092, 0.096), (0.092, 0.096)]
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        raw.append(tube("Leg", [f((x, 0.055, z)) for x, z in path], rad, "Hero_Bottom", segs=14, p=2.2, per=2))
    parts = [_fuse("TrousersShell", raw, voxel=0.010, z_lo=0.205, z_hi=0.995, tris=2200)]
    _belt(parts)
    for side in (1, -1):
        f = (lambda v: Vector(v)) if side == 1 else mir
        parts.append(_flat_band(f((0.1865, 0.055, 0.2065)), 0.092, 0.096, 0.0075, "Hero_Bottom", segs=14))
        parts.append(tube("Pipe", [f((0.190, 0.055, 0.940)), f((0.214, 0.055, 0.800)), f((0.250, 0.055, 0.610)), f((0.270, 0.055, 0.440)), f((0.272, 0.055, 0.300)), f((0.276, 0.055, 0.210))],
                          [(0.0045, 0.0045)] * 6, "Hero_Accent", segs=6, per=2))
    return join("Bottom_Trousers", parts)


def skirt():
    """A flared pleated skirt to mid-thigh, a belt at the waist and a trim at the hem."""
    prof = [(0.190, 0.995), (0.201, 0.93), (0.235, 0.82), (0.276, 0.72), (0.302, 0.664), (0.296, 0.656), (0.270, 0.714), (0.229, 0.814), (0.194, 0.925), (0.184, 0.990)]
    shell_ = lathe("Skirt", prof, "Hero_Bottom", segs=30, sx=1.0, sy=0.74, center=(0, SY, 0))
    parts = [shell_]
    _belt(parts)
    parts.append(_flat_band((0, SY, 0.662), 0.300, 0.300 * 0.74, 0.009, "Hero_Accent", segs=30))
    return join("Bottom_Skirt", parts)
