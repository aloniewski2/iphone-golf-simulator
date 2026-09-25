"""Hole 23 — Windmill Links (par 4): a cheerful flat island of tulip fields. A red windmill turns
beside the fairway; two canals cross it, each with a little white arched bridge — lay up short of
the first or carry it, and the second waits for a long drive. A pond with a jetty and a rowing
boat guards the green, a farmhouse with a red roof behind it; hedges and round trees.
Mock: blender/holes_mock/windmill_links.png (Higgsfield).

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 23, 4, "Windmill Links"
BLURB = "A par 4 through the tulip fields: two canals cross the fairway and a pond guards the green, with the windmill turning all the while."
GRID = 4.0
SEED = 20

GROUND = 7.0
CANALS = [-35.0, 85.0]            # y of each canal's middle
CANAL_HALF, CANAL_Z = 4.0, 6.1
POND = (58.0, 176.0, 17.0, 13.0)
POND_Z = 6.2


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


@lru_cache(maxsize=None)
def _height(x, y):
    z = GROUND + 0.35 * math.sin(x * 0.05 + 0.2) * math.cos(y * 0.04) + 0.2 * math.sin(y * 0.09)
    for cy in CANALS:
        z -= 2.6 * smoothstep((CANAL_HALF + 1.2 - abs(y - cy)) / 1.6)
    cx, cy, rx, ry = POND
    e = math.hypot((x - cx) / rx, (y - cy) / ry)
    z -= 2.4 * smoothstep((1.12 - e) / 0.2)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [[(-60, -200), (0, -206), (60, -198), (90, -170), (100, -110), (98, -40), (102, 30), (98, 100), (100, 170), (90, 218),
            (56, 244), (0, 250), (-56, 244), (-90, 220), (-100, 170), (-98, 100), (-102, 30), (-98, -40), (-100, -110), (-92, -170)]]

TEE = dict(cx=0, cy=-172, rx=11, ry=13, rot=0.0)
TEE_MARKER = (0.0, -178.0)
FAIRWAY_CL = [(0, -150, 17), (0, -100, 20), (0, -40, 20), (2, 20, 21), (4, 80, 20), (6, 130, 18), (10, 152, 13)]
GREENS = [dict(cx=14, cy=176, rx=19, ry=16, rot=math.radians(8))]
PIN = (16.0, 180.0)
BUNKERS = [
    (26, -60, 9, 6, 20, 1),
    (-24, 40, 9, 6, -15, 2),
    (-6, 198, 7, 5, 30, 3),
    (36, 158, 6, 4, 0, 4),
]
STAIRS = []
BRIDGES = []


def _canal(cy):
    return [(-88, cy - CANAL_HALF), (88, cy - CANAL_HALF), (88, cy + CANAL_HALF), (-88, cy + CANAL_HALF)]


POOLS = [dict(kind="water", outline=_canal(c), z=CANAL_Z) for c in CANALS] + \
        [dict(kind="water", cx=POND[0], cy=POND[1], rx=POND[2] - 1.5, ry=POND[3] - 1.5, z=POND_Z, wobble=0.06)]
WATER_HAZARDS = [(0, c, 88, CANAL_HALF) for c in CANALS] + [(POND[0], POND[1], POND[2] - 1.5, POND[3] - 1.5)]

LANDMARKS = [
    ("WINDMILL", -50, 14, -70),
    ("FARMHOUSE", -44, 214, 0),
    ("WHITE_BRIDGE", 0, CANALS[0], 90, 12),
    ("WHITE_BRIDGE", 4, CANALS[1], 90, 12),
    ("JETTY", 73, 176, 180, 9, POND_Z),
    ("BOAT", 56, 181, 20, POND_Z),
    ("TULIPS", -56, -108, 30, 44, 90),
    ("TULIPS", 54, -100, 26, 40, 90),
    ("TULIPS", 56, 30, 26, 40, 90),
    ("TULIPS", -58, 126, 26, 34, 90),
    ("TULIPS", 58, 118, 22, 28, 90),
    ("HEDGE", -22, -172, 90, 14),
    ("HEDGE", 22, -172, 90, 14),
    ("FENCE", -72, 196, [(-72, 196), (-12, 196), (-12, 236)]),
]

TREES = [
    (26, "TREE_ROUND", 10, None),
    (16, "BUSH_SMALL", 6, None),
    (6, "TREE_PINE_SMALL", 8, None),
]
