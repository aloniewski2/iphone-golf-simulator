"""Hole 07 design data: outlines, height field, centrelines. Pure math, no bpy side effects."""
import math, random
import hole07_lib as H
from mathutils import Vector

# ============================================================ design data
# Island outline — control polygon, clockwise from the south tip. Tee sits in the
# broad south-west lobe, a cove bites in on the east at mid-hole, the north headland
# narrows toward the clubhouse.
ISLAND = [
    (-22, -236), (24, -232), (64, -208), (80, -168), (66, -122), (72, -84),
    (56, -46), (30, -10), (46, 26), (86, 56), (78, 98), (58, 134), (84, 166),
    (66, 204), (46, 234), (16, 262), (-18, 254), (-44, 230), (-60, 196),
    (-64, 156), (-88, 116), (-70, 76), (-92, 34), (-98, -6), (-112, -48),
    (-104, -102), (-90, -146), (-76, -188), (-48, -220),
]

# Fairway centreline (tee -> green) with half-widths.
FAIRWAY_CL = [(-6, -148, 18), (-2, -105, 22), (-6, -55, 25), (-14, -5, 25),
              (-8, 45, 24), (2, 92, 22), (6, 128, 20), (6, 164, 15)]

GREEN = dict(cx=5, cy=182, rx=28, ry=23, rot=math.radians(8))
TEE = dict(cx=-6, cy=-188, rx=14, ry=20, rot=math.radians(-4))

# Bunkers: (cx, cy, rx, ry, rot_deg, seed)
BUNKERS = [
    (-44, 98, 16, 10, 20, 11),    # left fairway, upper (kidney pair)
    (-34, 62, 12, 8, -10, 12),    # left fairway, lower
    (40, 38, 15, 10, 15, 13),     # right fairway
    (-38, 156, 13, 9, 30, 14),    # left greenside
    (44, 148, 14, 10, -20, 15),   # right greenside (guards water side)
    (40, 200, 11, 8, 10, 16),     # right/upper greenside
]

SEED = 7

# ============================================================ terrain height field
def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

def height(x, y):
    # base slope: tee end lower, green end higher, then a broad valley mid-hole so the
    # fairway rolls away from the tee and climbs to the green (cliff tops ~27m -> ~38m)
    z = 27.0 + 11.0 * smoothstep((y + 230) / 480.0)
    z -= 3.2 * math.exp(-((y + 30) ** 2) / (2 * 95.0 ** 2))
    # gentle rolling mounds in the rough
    z += 0.9 * math.sin(x * 0.045 + 0.4) * math.cos(y * 0.028)
    z += 0.5 * math.sin(y * 0.09 + x * 0.02) * math.cos(x * 0.07)
    # green mound (elevated, guarded)
    d2 = (x - GREEN["cx"]) ** 2 + (y - GREEN["cy"]) ** 2
    z += 3.0 * math.exp(-d2 / (2 * 42.0 ** 2))
    # tee plateau
    d2 = (x - TEE["cx"]) ** 2 + (y - TEE["cy"]) ** 2
    z += 1.6 * math.exp(-d2 / (2 * 22.0 ** 2))
    # west shoulder so the east/water side reads as the exposed, lower edge
    z += 1.2 * smoothstep((-x - 20) / 60.0)
    return z


# Cart path centreline: west side, tee -> clubhouse
PATH_CL = [(-26, -216), (-44, -176), (-62, -130), (-70, -84), (-58, -40), (-66, 4),
           (-54, 44), (-62, 88), (-48, 124), (-42, 156), (-30, 188), (-18, 210), (-8, 224)]
CLUBHOUSE = dict(cx=8, cy=236, w=38, d=22, rot=math.radians(-14))

def island_outline():
    rnd = random.Random(SEED)
    outline = H.chaikin(ISLAND, 2)
    outline = H.resample(outline, 7.0)
    c = sum(outline, Vector((0, 0))) / len(outline)
    jittered = []
    for p in outline:
        d = (p - c).normalized()
        jittered.append(p + d * rnd.uniform(-2.5, 2.5))
    outline = jittered
    if H.signed_area(outline) < 0:
        outline.reverse()
    return outline, rnd

def fairway_centreline():
    cl = H.catmull_rom([(x, y) for x, y, w in FAIRWAY_CL], 10)
    widths = []
    nseg = len(FAIRWAY_CL) - 1
    for i in range(len(cl)):
        t = i / (len(cl) - 1) * nseg
        k = min(int(t), nseg - 1)
        f = t - k
        widths.append(FAIRWAY_CL[k][2] * (1 - f) + FAIRWAY_CL[k + 1][2] * f)
    return cl, widths

def fairway_outline(extra=0.0):
    cl, widths = fairway_centreline()
    left, right = [], []
    for i, p in enumerate(cl):
        p0 = cl[max(0, i - 1)]; p1 = cl[min(len(cl) - 1, i + 1)]
        t = (p1 - p0).normalized()
        n = Vector((-t.y, t.x))
        end_t = min(i, len(cl) - 1 - i) / 6.0
        w = (widths[i] + extra) * (math.sin(min(1.0, end_t) * math.pi / 2) * 0.9 + 0.1)
        left.append(p + n * w)
        right.append(p - n * w)
    poly = left + right[::-1]
    return H.chaikin(poly, 1)

STRIPES = 7
def stripe_index(x, y):
    """which mowing stripe a point falls in, by nearest centreline sample."""
    cl, _ = fairway_centreline()
    best, bi = 1e9, 0
    for i, p in enumerate(cl):
        d = (p.x - x) ** 2 + (p.y - y) ** 2
        if d < best:
            best, bi = d, i
    return int(bi / len(cl) * STRIPES)

def offset_outline(outline, dist, wobble=0.0, freq=0.5, seed=0):
    pts = [Vector((p.x, p.y)) for p in outline]
    c = sum(pts, Vector((0, 0))) / len(pts)
    n = len(pts)
    out = []
    for i, p in enumerate(pts):
        p0 = pts[i - 1]; p1 = pts[(i + 1) % n]
        t = (p1 - p0).normalized()
        nrm = Vector((t.y, -t.x))
        if nrm.dot(p - c) < 0:
            nrm = -nrm
        d = dist + wobble * (0.6 * math.sin(i * freq + seed) + 0.4 * math.sin(i * freq * 2.3 + seed * 1.7))
        out.append(p + nrm * d)
    return H.chaikin(out, 1)

def band_object(name, inner, outer, z, mat, col="ENVIRONMENT"):
    ob = H.new_mesh_object(name, col)
    n = len(inner)
    verts = [(p.x, p.y, z) for p in inner] + [(p.x, p.y, z) for p in outer]
    faces = [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    H.build_mesh(ob, verts, faces, smooth=True)
    H.assign(ob, mat)
    return ob


def path_points(samples=12):
    return H.catmull_rom(PATH_CL, samples)

def green_outline():
    return H.chaikin(H.ellipse(GREEN["cx"], GREEN["cy"], GREEN["rx"], GREEN["ry"], GREEN["rot"], 40, wobble=0.04, seed=3), 1)

def tee_outline():
    return H.chaikin(H.ellipse(TEE["cx"], TEE["cy"], TEE["rx"], TEE["ry"], TEE["rot"], 24), 1)

def bunker_outlines():
    return [H.blob(cx, cy, rx, ry, math.radians(rot), seed=seed, n=9, wobble=0.22, smooth=2)
            for (cx, cy, rx, ry, rot, seed) in BUNKERS]
