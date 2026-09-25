"""Hole 19 — Temple Falls (par 4): a jungle island on two levels. A waterfall pours off a mossy
cliff into a lagoon that cuts right across the fairway at the foot of the upper level, so the
drive has to carry the water and the step; lay up short and it's a long second. An old stone
stair climbs between the levels, and a stepped temple stands behind the green, ruined columns
and a great stone head in the jungle. Palms and broad-leaved plants everywhere.
Mock: blender/holes_mock/temple_falls.png (Higgsfield).

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 19, 4, "Temple Falls"
BLURB = "A par 4 over the lagoon: carry the waterfall's pool and the stone step with the drive, then pitch to the green under the temple."
THEME = "jungle"
GRID = 3.5
SEED = 19

LOW, HIGH = 11.0, 23.0
STEP_Y0, STEP_Y1 = 9.0, 19.0
LAGOON = (-6.0, -18.0, 60.0, 21.0)
LAGOON_BED, LAGOON_Z = 5.5, 7.5
CLIFF_TOP = 38.0


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


@lru_cache(maxsize=None)
def _height(x, y):
    wave = 7.0 * math.sin(x * 0.045 + 0.6) + 3.0 * math.sin(x * 0.11)   # the step's edge wanders
    z = LOW + (HIGH - LOW) * smoothstep((y - STEP_Y0 - wave) / (STEP_Y1 - STEP_Y0))
    z += 0.6 * math.sin(x * 0.06 + 0.3) * math.cos(y * 0.05) + 0.3 * math.sin(y * 0.1 + x * 0.04)
    # the lagoon's bowl
    cx, cy, rx, ry = LAGOON
    e = math.hypot((x - cx) / rx, (y - cy) / ry)
    z = z + (LAGOON_BED - z) * smoothstep((1.15 - e) / 0.25)
    # the falls' cliff: a high block on the west side of the lagoon
    dc = math.hypot(x + 70, (y - 0) / 1.35)
    block = smoothstep((30 - dc) / 8.0)
    z = z + (CLIFF_TOP - z) * block
    # the temple's knoll
    z += 3.0 * smoothstep((34 - math.hypot(x, y - 232)) / 12.0)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [[(-44, -206), (8, -214), (52, -196), (74, -150), (80, -90), (92, -30), (96, 30), (90, 100), (96, 170), (84, 232),
            (52, 268), (8, 276), (-36, 266), (-70, 236), (-86, 180), (-92, 120), (-98, 60), (-96, 0), (-90, -60), (-84, -120),
            (-70, -176)]]

TEE = dict(cx=0, cy=-165, rx=11, ry=14, rot=0.0)
TEE_MARKER = (0.0, -170.0)
# the lower fairway, a thin strip under the lagoon and up the step, then the upper fairway
FAIRWAY_CL = [(0, -145, 16), (2, -110, 20), (0, -70, 21), (0, -46, 12), (0, -20, 4), (0, 12, 4), (0, 32, 14), (-2, 62, 21),
              (-6, 102, 21), (-8, 142, 18), (-10, 166, 13)]
GREENS = [dict(cx=-10, cy=190, rx=20, ry=16, rot=math.radians(-6))]
PIN = (-8.0, 193.0)
BUNKERS = [
    (-30, -84, 9, 6, 15, 1),
    (30, 72, 9, 6, -20, 2),
    (-34, 196, 6, 4, 30, 3),
    (14, 178, 6, 4, -30, 4),
]
STAIRS = [((42, -4), (42, 24), 5.0, "stone")]
BRIDGES = []

POOLS = [dict(kind="water", cx=LAGOON[0], cy=LAGOON[1], rx=LAGOON[2] - 2, ry=LAGOON[3] - 2, z=LAGOON_Z, wobble=0.08)]
FALLS = [dict(kind="water", x=-47, y=-4, top=height(-48, -4) - 0.2, bottom=LAGOON_Z, width=7.0, dir=(1, -0.5), lean=3.5)]
WATER_HAZARDS = [(LAGOON[0], LAGOON[1], LAGOON[2] - 2, LAGOON[3] - 2)]

LANDMARKS = [
    ("TEMPLE", 0, 236, -90, 30),
    ("COLUMNS", 44, 192, 4, 18, 90),
    ("COLUMNS", -58, 118, 3, 12, 60),
    ("STONE_HEAD", -58, -140, 0),
]

TREES = [
    (26, "PALM", 9, None),
    (12, "PALM_TALL", 12, None),
    (34, "JUNGLE_BUSH", 5, None),
    (8, "TREE_ROUND", 12, None),
    (10, "JUNGLE_BUSH", 4, lambda x, y: math.hypot(x + 70, y / 1.35) < 24),   # the falls' cliff, overgrown
]
