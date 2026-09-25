"""Hole 17 — The Lagoon (par 3): from a tee on its own islet across the lagoon to a green
guarded by three bunkers, the boardwalk round the left for the walk over.

Design data for course_builder.py. Metres; +Y north, +X east; water at z = 0.
"""
import math
from functools import lru_cache
from maple_bay_palette import PALETTE  # noqa: F401

NUMBER, PAR, NAME = 17, 3, "The Lagoon"
BLURB = "A par 3 across the lagoon: carry the water to a green ringed by sand, and mind the wind off the bay."
GRID = 3.0
SEED = 17


@lru_cache(maxsize=None)
def _height(x, y):
    if y < 0:   # the tee islet
        return 9.0 + 0.3 * math.cos(x * 0.12) * math.cos(y * 0.1)
    return 12.0 + 0.9 * math.sin(x * 0.05 + 0.3) * math.cos(y * 0.04) + 1.5 * max(0.0, (y - 150) / 40.0)


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [
    [(math.cos(a) * (20 + 1.5 * math.sin(3 * a)), -40 + math.sin(a) * (19 + 1.2 * math.cos(4 * a))) for a in [2 * math.pi * i / 16 for i in range(16)]],
    [(-46, 62), (-10, 56), (30, 60), (58, 78), (70, 110), (66, 150), (48, 182), (12, 196), (-24, 190), (-50, 166), (-60, 130), (-58, 92)],
]

TEE = dict(cx=0, cy=-40, rx=8, ry=10, rot=0.0)
TEE_MARKER = (0.0, -46.0)
FAIRWAY_CL = []
GREENS = [dict(cx=10, cy=112, rx=20, ry=16, rot=math.radians(10))]
PIN = (14.0, 114.0)
BUNKERS = [
    (-14, 96, 8, 5, 0, 1),
    (34, 120, 8, 5, 20, 2),
    (8, 136, 10, 4, 0, 3),
]
BRIDGES = [[(-12, -26, 9.3), (-34, 70, 12.3)]]
STAIRS = []
LANDMARKS = [("TOWER", 44, 164)]


def _main(x, y): return y > 40


def _islet(x, y): return y < 0


TREES = [
    (26, "TREE_ROUND", 8, _main),
    (8, "TREE_PINE_MEDIUM", 9, _main),
    (6, "TREE_ROUND", 6, _islet),
    (8, "BUSH_SMALL", 6, None),
]
