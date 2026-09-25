"""Hole 19 — Volcano Rim (par 4): a black basalt island under a smoking volcano. A river of lava
runs off the crater down the east flank and turns across the fairway, so the drive has to carry
it (a stone arch takes the path over); then the fairway follows the ridge right to a green on a
ledge under the crater. Black-sand bunkers, palms and burnt trees, steam vents.
Mock: blender/holes_mock/volcano_rim.png (Higgsfield).

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 19, 4, "Volcano Rim"
BLURB = "A par 4 under a smoking volcano: carry the river of lava off the tee, then follow the ridge right to a green on a ledge beneath the crater."
THEME = "volcano"
GRID = 4.0
SEED = 16

VX, VY, VR = 88.0, 250.0, 62.0         # the volcano: centre and the radius of its foot
V_RISE, CRATER_R = 62.0, 11.0
ROCK_ABOVE = 31.0                      # the cone is bare basalt above the plateau
GREEN_C = (32.0, 170.0)
LAVA = [(92, 232), (108, 200), (116, 158), (110, 110), (96, 62), (72, 16), (36, -8), (0, -14), (-40, -12), (-70, -8), (-90, -6)]


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def _seg_dist(px, py, a, b):
    ax, ay = a; bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - dx * t, py - ay - dy * t)


def lava_dist(x, y):
    return min(_seg_dist(x, y, a, b) for a, b in zip(LAVA, LAVA[1:]))


def cone(x, y):
    """The volcano's rise above the plateau."""
    d = math.hypot(x - VX, y - VY)
    if d >= VR: return 0.0
    rim = V_RISE * (1 - max(d, CRATER_R) / VR) ** 1.35
    if d < CRATER_R:   # the crater: a bowl sunk into the top
        rim -= 8.0 * smoothstep((CRATER_R - d) / 5.0)
    return rim


def base(x, y):
    z = 14.0 + 6.0 * smoothstep((y + 200) / 420.0)
    z += 0.9 * math.sin(x * 0.06 + 0.5) * math.cos(y * 0.035) + 0.5 * math.sin(y * 0.08 + x * 0.03)
    return z


@lru_cache(maxsize=None)
def _height(x, y):
    z = base(x, y)
    c = cone(x, y)
    z += c
    if c < 3.0:   # the lava's channel, where it runs over the flat
        z -= 1.3 * smoothstep((5.5 - lava_dist(x, y)) / 3.0) * (1 - c / 3.0)
    # the green's ledge: raised a few metres above the ridge
    dg = math.hypot(x - GREEN_C[0], y - GREEN_C[1])
    z += 3.5 * smoothstep((30.0 - dg) / 9.0)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [[(-40, -232), (20, -228), (60, -200), (78, -150), (86, -90), (106, -40), (120, 20), (132, 90), (150, 150), (164, 210),
            (166, 270), (146, 314), (104, 330), (60, 322), (28, 294), (2, 252), (-22, 208), (-56, 162), (-80, 110), (-96, 50),
            (-100, -10), (-94, -80), (-86, -140), (-72, -192)]]

TEE = dict(cx=0, cy=-195, rx=11, ry=14, rot=0.0)
TEE_MARKER = (0.0, -200.0)
FAIRWAY_CL = [(0, -170, 15), (-4, -110, 20), (-6, -50, 21), (-4, -14, 20), (-2, 22, 21), (4, 70, 21), (14, 108, 18), (22, 138, 15), (26, 150, 12)]
GREENS = [dict(cx=GREEN_C[0], cy=GREEN_C[1] + 2, rx=19, ry=16, rot=math.radians(10))]
PIN = (34.0, 176.0)
BUNKERS = [
    (-30, 70, 10, 6, 20, 1),
    (34, 96, 9, 6, -15, 2),
    (8, 192, 7, 5, 30, 3),
    (56, 160, 6, 5, 0, 4),
    (-22, -80, 9, 6, 10, 5),
]
STAIRS = []
BRIDGES = []

POOLS = [dict(kind="lava", cx=VX, cy=VY, rx=CRATER_R - 1.5, ry=CRATER_R - 1.5, z=height(VX, VY) + 0.8)]
RIVERS = [
    dict(kind="lava", points=LAVA, width=7.0, lift=0.25),
    dict(kind="lava", points=[(114, 170), (136, 166), (156, 160)], width=5.0, lift=0.25),
]
FALLS = [
    dict(kind="lava", x=-97, y=-6, top=height(-92, -6), bottom=0.0, width=5.0, dir=(-1, 0), lean=3.0),
    dict(kind="lava", x=160, y=160, top=height(156, 160), bottom=0.0, width=4.0, dir=(1, -0.1), lean=3.0),
]
# the lava in play, as ellipses (centre x, y, half-width, half-length) in metres
WATER_HAZARDS = [(8, -12, 92, 6), (100, 60, 16, 30), (112, 140, 10, 40), (VX, VY, CRATER_R, CRATER_R)]

LANDMARKS = [
    ("ARCH_BRIDGE", -4, -14, 90, 16, 7),
    ("SMOKE", VX, VY, 14.0, 7.5),
    ("STEAM_VENT", -62, 60),
    ("STEAM_VENT", 70, -100),
    ("STEAM_VENT", -44, 150),
]


def _flat(x, y):
    return cone(x, y) < 4.0


TREES = [
    (22, "PALM", 10, _flat),
    (10, "PALM_TALL", 12, _flat),
    (16, "DEAD_TREE", 9, None),
    (60, "BUSH_SMALL", 5, _flat),
]
