"""Hole 21 — Obsidian Slab (par 3): the Magma Open's short hole. A slab of black rock for a tee and, across
the crater's lava lake, a bigger slab with a raised green: a carry of about a hundred yards over molten rock,
with braziers on both slabs, fire geysers in the gap and basalt stacks standing in the lava either side.
The sea is lava (THEME "magma"), so anything that leaves the two slabs burns.

Design data for course_builder.py. Metres; +Y north, +X east; the lava lake at z = 0.
"""
import math
from functools import lru_cache
from magma_shapes import smoothstep

NUMBER, PAR, NAME = 21, 3, "Obsidian Slab"
BLURB = "A par 3 over the molten lake: carry the lava from a slab of black rock to a green on the far side."
THEME = "magma"
LAVA_SEA = True
ROUGH_WIDTH = 300   # each slab plays as rough to its edge: the lava decides, not the distance from the line
GRID = 4.0
SEED = 21

GREEN_C = (10.0, 120.0)


@lru_cache(maxsize=None)
def _height(x, y):
    if y < 48:   # the tee slab: a level plateau with the odd swell
        z = 18.0 + 0.7 * math.sin(x * 0.14 + 0.4) * math.cos(y * 0.12) + 0.3 * math.sin(y * 0.3)
    else:        # the far slab, a shade higher, its green on a ledge
        z = 21.0 + 0.9 * math.sin(x * 0.07 + 1.1) * math.cos(y * 0.06 + 0.3) + 0.5 * math.sin(y * 0.17 + x * 0.05)
        dg = math.hypot(x - GREEN_C[0], y - GREEN_C[1])
        z += 2.6 * smoothstep((32.0 - dg) / 9.0)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [
    # the tee slab
    [(-22, -44), (2, -48), (24, -40), (31, -14), (27, 14), (5, 27), (-22, 21), (-31, -8)],
    # the far slab
    [(-48, 82), (-12, 70), (30, 72), (57, 88), (68, 119), (54, 152), (17, 168), (-26, 162), (-56, 139), (-62, 108)],
]

TEE = dict(cx=0, cy=-24, rx=9, ry=12, rot=0.0)
TEE_MARKER = (0.0, -27.0)
# the fairway carpet is on the far slab, where the line comes down; the game measures the hole along it
FAIRWAY_CL = [(2, 80, 13), (7, 98, 16), (10, 110, 15)]
GREENS = [dict(cx=GREEN_C[0], cy=GREEN_C[1], rx=20, ry=17, rot=math.radians(15))]
PIN = (11.0, 124.0)
BUNKERS = [
    (-16, 104, 9, 6, 20, 1),
    (36, 116, 8, 6, -15, 2),
    (12, 145, 9, 6, 10, 3),
    (28, 95, 6, 4.5, 0, 4),
]
STAIRS = []
BRIDGES = []
POOLS = []
RIVERS = []
FALLS = []
WATER_HAZARDS = []

LANDMARKS = [
    ("BRAZIER", -12, -32),
    ("BRAZIER", 12, -32),
    ("BRAZIER", -30, 100),
    ("BRAZIER", 46, 134),
    ("GEYSER", -34, 46, 4.5),
    ("GEYSER", 36, 40, 4.0),
    ("STACKS", 0, 46, 40, 68, 18),
    ("STEAM_VENT", -44, 128),
    ("STEAM_VENT", 52, 100),
]

TREES = [
    (7, "DEAD_TREE", 10, lambda x, y: y > 60 and abs(x - 8) > 20),
    (5, "PALM", 12, lambda x, y: y > 60 and abs(x - 8) > 26),
    (6, "OBSIDIAN_SPIRE", 15, lambda x, y: abs(x - 6) > 24),
    (12, "BUSH_SMALL", 5, None),
]
