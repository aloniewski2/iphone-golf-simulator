"""Hole 15 — The Steps (par 3), after the step green in Rising Impact: from a tee on its own islet,
across the water and a boardwalk, to one huge green built as three terraces. The pin is on the
middle tier; land on the wrong step and the putt has to climb or run down it.
Mock: blender/holes_mock/the_steps.png.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache

NUMBER, PAR, NAME = 15, 3, "The Steps"
BLURB = "A par 3 over the water to a green of three tiers: the pin is on the middle step, so land on it or putt up and down the stairs."
GRID = 3.0
SEED = 15

TIERS = [(72.0, 1.1), (108.0, 1.1)]     # (y of each riser, its rise in metres)
RISER = 3.0                              # metres a riser takes to climb
Z_TIER1 = 15.0


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


@lru_cache(maxsize=None)
def _height(x, y):
    if y < 0:   # the tee islet
        return 11.0 + 0.3 * math.cos(x * 0.1) * math.cos(y * 0.1)
    z = Z_TIER1 - 0.6 * smoothstep((34 - y) / 14.0)   # the apron falls away at the front
    for ry, rise in TIERS:
        z += rise * smoothstep((y - ry + RISER / 2) / RISER)
    # behind the top tier the ground climbs to a knoll for the lighthouse and the pines
    z += 3.5 * smoothstep((y - 150) / 22.0)
    # the sides roll off a little outside the green
    z -= 0.8 * smoothstep((abs(x) - 38) / 10.0)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


def _round_rect(x0, x1, y0, y1, r, n=6):
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


ISLANDS = [
    [(math.cos(a) * (21 + 1.5 * math.sin(4 * a)), -40 + math.sin(a) * (20 + 1.5 * math.cos(3 * a))) for a in [2 * math.pi * i / 16 for i in range(16)]],
    [(-44, 18), (-8, 12), (26, 16), (48, 30), (54, 70), (52, 112), (56, 150), (44, 176), (14, 190), (-22, 186), (-48, 168), (-56, 130), (-52, 88), (-56, 48)],
]

TEE = dict(cx=0, cy=-40, rx=9, ry=11, rot=0.0)
TEE_MARKER = (0.0, -46.0)
FAIRWAY_CL = []
GREENS = [dict(outline=_round_rect(-37, 37, 40, 144, 14))]
PIN = (8.0, 90.0)
# the whole terrace is putting green: the flight model's round green has to reach every tier
GREEN_RADIUS_YD = 50
BUNKERS = [
    (-36, 32, 9, 5, 12, 1),
    (34, 33, 9, 5, -12, 2),
]
# the boardwalk from the tee islet to the main island
BRIDGES = [[(0, -21, 11.3), (0, 24, 14.6)]]
# little wooden stairs up the side of each riser
STAIRS = [((42, ry - RISER), (42, ry + RISER), 2.2) for ry, _ in TIERS]
LANDMARKS = [("LIGHTHOUSE", -26, 168)]


def _behind(x, y): return y > 150


def _islet(x, y): return y < 0


TREES = [
    (18, "TREE_PINE_LARGE", 8, _behind),
    (10, "TREE_PINE_MEDIUM", 7, _behind),
    (10, "TREE_PINE_LARGE", 6, _islet),
    (8, "TREE_PINE_SMALL", 6, _islet),
    (16, "TREE_PINE_MEDIUM", 9, None),
    (8, "BUSH_SMALL", 6, None),
]
