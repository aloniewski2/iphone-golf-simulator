"""Hole 18 — Mesa Canyon (par 3): two flat-topped mesas of banded red rock stand out of the sea
with a canyon of water between them. The tee is on one, the green on the other; a rope bridge
hangs across the chasm. Carry it or it's in the river. Saguaros, barrel cacti, desert tufts, a
waterfall off the green's mesa into the canyon.
Mock: blender/holes_mock/mesa_canyon.png (Higgsfield).

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 18, 3, "Mesa Canyon"
BLURB = "A par 3 from one mesa to the next over a canyon of water: carry the chasm to a green on the far mesa's top, cacti all round."
THEME = "desert"
GRID = 3.0
SEED = 18

TEE_TOP, GREEN_TOP = 34.0, 36.0


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


@lru_cache(maxsize=None)
def _height(x, y):
    top = TEE_TOP if y < 50 else GREEN_TOP
    return top + 0.5 * math.sin(x * 0.08 + 0.4) * math.cos(y * 0.06) + 0.3 * math.sin(y * 0.11 + x * 0.05)


def height(x, y):
    return _height(round(x, 2), round(y, 2))


def _mesa(cx, cy, rx, ry, n, seed):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        w = 1 + 0.07 * math.sin(3 * a + seed) + 0.05 * math.sin(5 * a + 2 * seed)
        pts.append((cx + math.cos(a) * rx * w, cy + math.sin(a) * ry * w))
    return pts


ISLANDS = [
    _mesa(0, -12, 44, 42, 20, 1),       # the tee's mesa
    _mesa(18, 124, 58, 56, 24, 2),      # the green's mesa, across the canyon
]

TEE = dict(cx=0, cy=-24, rx=9, ry=11, rot=0.0)
TEE_MARKER = (0.0, -30.0)
FAIRWAY_CL = []
GREENS = [dict(cx=14, cy=118, rx=21, ry=17, rot=math.radians(-8))]
PIN = (15.0, 121.0)
BUNKERS = [
    (40, 120, 8, 6, 20, 1),
    (-12, 138, 6, 4, -20, 2),
]
STAIRS = []
BRIDGES = []

FALLS = [dict(kind="water", x=-26, y=74, top=GREEN_TOP - 0.3, bottom=0.2, width=4.5, dir=(-0.5, -1), lean=3.0)]

LANDMARKS = [
    ("ROPE_BRIDGE", -16, 26, -10, 72, TEE_TOP + 0.2, GREEN_TOP + 0.2),
]

TREES = [
    (10, "SAGUARO", 9, None),
    (8, "SAGUARO_SMALL", 7, None),
    (16, "BARREL_CACTUS", 4, None),
    (30, "DRY_TUFT", 3, None),
]
