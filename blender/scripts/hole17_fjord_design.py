"""Hole 17 — Frostbite Fjord (par 5): a winter island under snow, the fairway cleared green and
curving left then right round a frozen lake. Cut the corner over the ice for a shorter second,
or play round it. Snow-laden pines, a log cabin with its chimney going and a snowman by the
tee, a frozen stream to a frozen fall off the cliffs, ice floes in the sea, icicles on the lip.
Mock: blender/holes_mock/frostbite_fjord.png (Higgsfield).

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 17, 5, "Frostbite Fjord"
BLURB = "A long par 5 round a frozen lake: play it safe along the pines, or cut the corner over the ice for a shot at the green in two."
THEME = "snow"
ICICLES = True
GRID = 4.0
SEED = 17

LAKE = (45.0, 20.0, 50.0, 84.0)          # centre x, y, half-width, half-length
LAKE_BED, ICE_Z = 8.0, 9.4
STREAM = [(92, 36), (112, 44), (126, 50)]


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def lake_e(x, y):
    cx, cy, rx, ry = LAKE
    return math.hypot((x - cx) / rx, (y - cy) / ry)


def _seg_dist(px, py, a, b):
    ax, ay = a; bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - dx * t, py - ay - dy * t)


@lru_cache(maxsize=None)
def _height(x, y):
    z = 14.0 + 5.0 * smoothstep((y + 250) / 520.0)
    z += 0.8 * math.sin(x * 0.05 + 0.2) * math.cos(y * 0.04) + 0.4 * math.sin(y * 0.09 + x * 0.03)
    # the lake's basin, a bank round it
    e = lake_e(x, y)
    z = z + (LAKE_BED - z) * smoothstep((1.12 - e) / 0.22)
    # the stream's cut from the lake to the cliffs
    ds = min(_seg_dist(x, y, a, b) for a, b in zip(STREAM, STREAM[1:]))
    if e > 0.95:
        z -= (z - (ICE_Z - 0.6)) * smoothstep((6.0 - ds) / 3.0)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [[(math.cos(a) * (126 + 8 * math.sin(3 * a + 0.3) + 5 * math.sin(7 * a)), 10 + math.sin(a) * (262 + 9 * math.sin(2 * a + 0.8) + 6 * math.cos(5 * a)))
            for a in [2 * math.pi * i / 32 for i in range(32)]]]

TEE = dict(cx=-55, cy=-215, rx=11, ry=14, rot=math.radians(-6))
TEE_MARKER = (-55.0, -220.0)
FAIRWAY_CL = [(-55, -190, 16), (-68, -130, 20), (-74, -60, 22), (-70, 10, 22), (-58, 80, 22), (-36, 140, 21), (-4, 190, 19), (30, 222, 16), (52, 236, 13)]
GREENS = [dict(cx=74, cy=246, rx=20, ry=17, rot=math.radians(-20))]
PIN = (76.0, 249.0)
BUNKERS = [
    (-42, -60, 9, 6, 10, 1),
    (-102, 40, 10, 6, -20, 2),
    (-14, 152, 9, 6, 15, 3),
    (96, 232, 7, 5, 0, 4),
    (58, 264, 6, 4, 30, 5),
]
STAIRS = []
BRIDGES = []

POOLS = [dict(kind="ice", cx=LAKE[0], cy=LAKE[1], rx=LAKE[2] - 3, ry=LAKE[3] - 3, z=ICE_Z, wobble=0.04)]
RIVERS = [dict(kind="ice", points=[(88, 34)] + STREAM, width=6.0, lift=0.3)]
FALLS = [dict(kind="ice", x=130, y=51, top=height(126, 50) + 0.2, bottom=0.2, width=6.0, dir=(1, 0.15), lean=1.5)]
WATER_HAZARDS = [(LAKE[0], LAKE[1], LAKE[2] - 3, LAKE[3] - 3), (108, 43, 20, 5)]

LANDMARKS = [
    ("CABIN", -98, -206, 15),
    ("SNOWMAN", -28, -206),
    ("WHITE_BRIDGE", 110, 44, 100, 11, "wood"),
    ("ICE_FLOES", 0, 10, 26, 150, 330),
]

TREES = [
    (46, "SNOW_PINE_LARGE", 12, None),
    (30, "SNOW_PINE_MEDIUM", 9, None),
    (22, "SNOW_PINE_SMALL", 7, None),
    (8, "SNOW_PINE_MEDIUM", 7, lambda x, y: 1.15 < lake_e(x, y) < 1.4),   # a fringe of pines round the ice
]
