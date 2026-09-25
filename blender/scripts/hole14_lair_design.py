"""Hole 14 — The Witch's Lair (par 4), after the hole in Rising Impact that Gawain chips in on:
the green lies at the bottom of a crater walled by rock and dark pines, reached through one gap
at the front; a ruined tower stands on the rim. Mock: blender/holes_mock/witchs_lair.png.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 14, 4, "The Witch's Lair"
BLURB = "A par 4 to a green sunk in a crater: find the gap in the rim, or chip down over the pines into the lair."
GRID = 4.0
SEED = 14

CX, CY = 0.0, 150.0            # the crater
R_FLOOR, R_RIM = 32.0, 48.0
Z_FLOOR, RIM_RISE = 13.0, 5.0
GAP = 0.5                      # half-width of the gap in the rim, radians, facing the tee


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def base(x, y):
    z = 24.0 + 8.0 * smoothstep((y + 210) / 330.0)
    z += 0.8 * math.sin(x * 0.05 + 0.4) * math.cos(y * 0.03) + 0.4 * math.sin(y * 0.09 + x * 0.02)
    return z


@lru_cache(maxsize=None)
def _height(x, y):
    z = base(x, y)
    dx, dy = x - CX, y - CY
    d = math.hypot(dx, dy)
    if d > R_RIM + 14:
        return z
    a = abs(math.atan2(dx, -dy))                  # 0 facing the tee (south)
    gap = smoothstep((GAP - a) / 0.25)            # 1 in the gap, 0 on the rim
    floor = Z_FLOOR + 0.6 * (d / R_FLOOR) ** 2
    rim_top = z + RIM_RISE * math.exp(-((d - R_RIM) / 6.0) ** 2)
    if d <= R_FLOOR:
        walled = floor
    else:
        walled = floor + (rim_top - floor) * smoothstep((d - R_FLOOR) / (R_RIM - R_FLOOR)) ** 0.7
    # the gap: a long even ramp from the fairway down to the floor
    ramp = Z_FLOOR + 0.6 + (z - Z_FLOOR - 0.6) * smoothstep((d - R_FLOOR + 4) / (R_RIM + 18 - R_FLOOR + 4))
    return walled * (1 - gap) + ramp * gap


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [[(-26, -232), (22, -230), (58, -204), (70, -160), (60, -110), (72, -60), (62, -10), (74, 40),
            (86, 96), (92, 150), (84, 198), (56, 226), (18, 240), (-24, 236), (-62, 218), (-88, 184),
            (-94, 136), (-80, 90), (-64, 44), (-76, -4), (-68, -58), (-78, -108), (-70, -160), (-52, -206)]]

TEE = dict(cx=-2, cy=-190, rx=12, ry=16, rot=math.radians(-3))
TEE_MARKER = (TEE["cx"], TEE["cy"] - 5)
FAIRWAY_CL = [(-2, -166, 15), (-8, -110, 20), (-2, -50, 22), (8, 10, 22), (6, 60, 19), (2, 96, 15), (0, 116, 12), (0, 128, 11)]
GREENS = [dict(cx=0, cy=151, rx=20, ry=18, rot=math.radians(6))]
PIN = (3.0, 155.0)
BUNKERS = [
    (-34, -40, 11, 7, 20, 1),
    (30, 22, 12, 8, -15, 2),
    (17, 98, 5, 5, 0, 3),       # the pot bunker at the gap
    (-15, 164, 6, 4, 40, 4),
]
STAIRS = []
BRIDGES = []
LANDMARKS = [("TOWER", CX + 36, CY + 34)]


def _rim(x, y):
    d = math.hypot(x - CX, y - CY)
    a = abs(math.atan2(x - CX, -(y - CY)))
    return R_RIM - 7 < d < R_RIM + 14 and a > GAP + 0.1


TREES = [
    (70, "TREE_PINE_LARGE", 7, _rim),           # the lair's wall of pines
    (30, "TREE_PINE_MEDIUM", 7, _rim),
    (26, "TREE_PINE_LARGE", 13, None),
    (18, "TREE_PINE_MEDIUM", 11, None),
    (12, "TREE_ROUND", 12, None),
    (10, "BUSH_SMALL", 7, None),
]
DARK_TREES = True    # the lair's pines are the darker green
