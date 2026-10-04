"""Hole 23 — Caldera Crown (par 5): the Magma Open's big hole. A horseshoe of black rock around a lagoon of
lava with a fire geyser in its middle. The tee is on one tip of the horseshoe and the green on the other, only
about a hundred and fifty yards apart across the lagoon: play the long way round (three shots along the ridge, a neck in the
middle where the lava crowds in) or gamble on the carry. The sea is lava, so anything that leaves the rock burns.

Design data for course_builder.py. Metres; +Y north, +X east; the lava lake at z = 0.
"""
import math
from functools import lru_cache
from magma_shapes import ribbon, catmull, dist_to_polyline, smoothstep, frame, place, heading, polar

NUMBER, PAR, NAME = 23, 5, "Caldera Crown"
BLURB = "A par 5 round a lagoon of lava: three shots along the ridge, or gamble on the carry straight across."
THEME = "magma"
LAVA_SEA = True
ROUGH_WIDTH = 300
GRID = 4.0
SEED = 23

CX, CY, R0 = 0.0, 124.0, 104.0       # the lagoon's centre and the ridge's radius
START, END = -130.0, -50.0          # the tee's and the green's angle (degrees): an 80 degree gap between them
SWEEP = 360.0 - abs(END - START)    # 310 degrees of ridge
STEPS = 11


def ring_point(deg, r=R0):
    a = math.radians(deg)
    return (CX + math.cos(a) * r, CY + math.sin(a) * r)


# clockwise from the tee (angle falling) round the west, the north and the east to the green
ANGLES = [START - k * SWEEP / (STEPS - 1) for k in range(STEPS)]   # START, ..., END - 360
SPINE = [ring_point(a) for a in ANGLES]
#          tee pad landing1(wide)  ....  neck (narrow, north)  ....  landing2 (wide)  approach plateau
WIDTHS = [18, 22, 30, 28, 20, 14, 18, 28, 30, 26, 34]
SPINE_CURVE = catmull(SPINE, 12)
F = frame(SPINE, WIDTHS)
HEAD_END = heading(F, 0.99)
HEAD_START = heading(F, 0.01)
GREEN_C = ring_point(END - 360.0 + 360.0, R0)   # (the same place as END)


@lru_cache(maxsize=None)
def _height(x, y):
    d, t = dist_to_polyline(x, y, SPINE_CURVE)
    z = 17.0 + 1.4 * math.sin(x * 0.07 + 0.6) * math.cos(y * 0.06) + 0.7 * math.sin(y * 0.15 + x * 0.05)
    z += 1.8 * math.sin(t * 8.0 + 0.5)
    # the ridge is a little higher on the outside than on the lagoon's side
    r = math.hypot(x - CX, y - CY)
    z += 1.3 * smoothstep((r - R0) / 24.0)
    dg = math.hypot(x - GREEN_C[0], y - GREEN_C[1])
    z += 3.0 * smoothstep((30.0 - dg) / 10.0)
    return z


def height(x, y):
    return _height(round(x, 2), round(y, 2))


ISLANDS = [ribbon(SPINE, WIDTHS, per=6)]

_tee = ring_point(START)
TEE = dict(cx=_tee[0], cy=_tee[1], rx=11, ry=14, rot=math.radians(HEAD_START - 90))
TEE_MARKER = ring_point(START + 3.0, R0)
# the fairway: the ridge's spine, from just past the tee pad to the approach
FAIRWAY_CL = [(ring_point(a)[0], ring_point(a)[1], w) for a, w in zip(ANGLES[1:-1], [17, 20, 18, 12, 9, 12, 18, 20, 16])]
_g = ring_point(END)
GREENS = [dict(cx=_g[0], cy=_g[1], rx=21, ry=18, rot=math.radians(HEAD_END - 90))]
PIN = (_g[0] + 2.0, _g[1] + 3.0)


def _b(p, rx, ry, rot, seed):
    return (p[0], p[1], rx, ry, rot, seed)


BUNKERS = [
    _b(place(F, .14, .55), 9, 6.5, 20, 1),
    _b(place(F, .22, -.55), 9, 6, -10, 2),
    _b(place(F, .34, .55), 10, 6.5, 40, 3),
    _b(place(F, .60, -.48), 9, 6, 60, 4),
    _b(place(F, .74, .55), 8, 6, -30, 5),
    _b(polar(_g[0], _g[1], HEAD_END + 145, 31), 8, 6, 10, 6),
    _b(polar(_g[0], _g[1], HEAD_END - 145, 31), 7, 5.5, -20, 7),
]
STAIRS = []
BRIDGES = []
_p1, _p2 = place(F, .17, -.58), place(F, .67, .58)
POOLS = [
    dict(kind="lava", cx=_p1[0], cy=_p1[1], rx=8.0, ry=6.0, z=16.6),
    dict(kind="lava", cx=_p2[0], cy=_p2[1], rx=8.0, ry=5.5, z=16.8),
]
RIVERS = []
FALLS = []
WATER_HAZARDS = [(_p1[0], _p1[1], 8.0, 6.0, "lava"), (_p2[0], _p2[1], 8.0, 5.5, "lava")]

LANDMARKS = [
    ("BRAZIER", *place(F, .01, .75)),
    ("BRAZIER", *place(F, .01, -.75)),
    ("BRAZIER", *polar(_g[0], _g[1], HEAD_END + 100, 27)),
    ("BRAZIER", *polar(_g[0], _g[1], HEAD_END - 100, 27)),
    ("GEYSER", CX, CY, 7.0),
    ("GEYSER", CX + 22, CY - 20, 4.0),
    ("GEYSER", CX - 24, CY + 16, 4.0),
    ("GEYSER", *place(F, .38, 2.4), 5.0),
    ("STACKS", CX, CY - 18, 0, 14, 20),
    ("STACKS", *place(F, .30, 2.5), 0, 22, 24),
    ("STACKS", *place(F, .52, 2.4), 0, 22, 24),
    ("STACKS", *place(F, .70, 2.5), 0, 22, 24),
    ("STEAM_VENT", *place(F, .28, .5)),
    ("STEAM_VENT", *place(F, .62, .5)),
]

TREES = [
    (12, "DEAD_TREE", 13, None),
    (7, "PALM", 15, None),
    (8, "OBSIDIAN_SPIRE", 18, None),
    (16, "BUSH_SMALL", 6, None),
]
